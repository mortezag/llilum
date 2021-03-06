//
// Copyright (c) Microsoft Corporation.    All rights reserved.
//

namespace Microsoft.Zelig.Runtime
{
    using System;
    using System.Runtime.CompilerServices;

    using TS = Microsoft.Zelig.Runtime.TypeSystem;


    [ImplicitInstance]
    [ForceDevirtualization]
    [TS.WellKnownType( "Microsoft_Zelig_Runtime_ThreadManager" )]
    public abstract class ThreadManager
    {
        class EmptyManager : ThreadManager
        {
            //
            // State
            //

            //
            // Helper Methods
            //

            public override void InitializeBeforeStaticConstructors()
            {
            }

            public override void InitializeAfterStaticConstructors( uint[] systemStack )
            {
            }

            public override void Activate()
            {
            }

            public override void Reschedule()
            {
            }

            public override void SetNextWaitTimer( SchedulerTime nextTimeout )
            {
            }

            public override void CancelQuantumTimer()
            {
            }

            public override void SetNextQuantumTimer()
            {
            }

            public override void SetNextQuantumTimer( SchedulerTime nextTimeout )
            {
            }
        }

        //
        // State
        //

        protected KernelList< ThreadImpl >        m_allThreads;
        protected KernelList< ThreadImpl >        m_readyThreads;
        protected KernelList< ThreadImpl >        m_waitingThreads;
                                          
        protected ThreadImpl                      m_mainThread;
        protected ThreadImpl                      m_idleThread;
        protected ThreadImpl                      m_interruptThread;
        protected ThreadImpl                      m_fastInterruptThread;
        protected ThreadImpl                      m_abortThread;
        protected ThreadImpl                      m_undefThread;
        protected EventWaitHandleImpl             m_neverSignaledEvent;
        protected bool                            m_noInvalidateNextWaitTimerRecursion;
                                          
        //--//                            
                                          
        protected ThreadImpl                      m_runningThread;
        protected ThreadImpl                      m_nextThread;
                        
        protected KernelPerformanceCounter        m_deadThreadsTime;

        //
        // Helper Methods
        //

        public virtual void InitializeBeforeStaticConstructors()
        {
            //
            // Create the first active thread.
            //
            m_mainThread = new ThreadImpl( MainThread, new uint[1024] );

            //
            // We need to have a current thread during initialization, in case some static constructors try to access it.
            //
            ThreadImpl.CurrentThread = m_mainThread;
        }

        public virtual void InitializeAfterStaticConstructors( uint[] systemStack )
        {
            m_allThreads          = new KernelList< ThreadImpl >();
            m_readyThreads        = new KernelList< ThreadImpl >();
            m_waitingThreads      = new KernelList< ThreadImpl >();

            m_idleThread          = new ThreadImpl( IdleThread, systemStack );
            m_interruptThread     = new ThreadImpl( null, new uint[512] );
            m_fastInterruptThread = new ThreadImpl( null, new uint[512] );
            m_abortThread         = new ThreadImpl( null, new uint[128] );
            m_undefThread         = new ThreadImpl( null, new uint[128] );
            m_neverSignaledEvent  = new EventWaitHandleImpl( false, System.Threading.EventResetMode.ManualReset );

            //
            // These threads are never started, so we have to manually register them, to enable the debugger to see them.
            //
            RegisterThread( m_idleThread          );
            RegisterThread( m_interruptThread     );
            RegisterThread( m_fastInterruptThread );
            RegisterThread( m_abortThread         );

            //--//

            m_interruptThread    .SetupForExceptionHandling( TargetPlatform.ARMv4.ProcessorARMv4.c_psr_mode_IRQ   );
            m_fastInterruptThread.SetupForExceptionHandling( TargetPlatform.ARMv4.ProcessorARMv4.c_psr_mode_FIQ   );
            m_abortThread        .SetupForExceptionHandling( TargetPlatform.ARMv4.ProcessorARMv4.c_psr_mode_ABORT );
            m_undefThread        .SetupForExceptionHandling( TargetPlatform.ARMv4.ProcessorARMv4.c_psr_mode_UNDEF );
        }

        public virtual void Activate()
        {
        }

        [NoReturn]
        public virtual void StartThreads()
        {
            //
            // 'm_runningThread' should never be null once the interrupts have been enabled, so we have to set it here.
            //
            ThreadImpl bootstrapThread = m_idleThread;

            m_runningThread = bootstrapThread;
            bootstrapThread.AcquiredProcessor();

            //
            // Start the first active thread.
            //
            m_mainThread.Start();

            //
            // Long jump to the idle thread context, which will reenable interrupts and cause the first context switch.
            //
            bootstrapThread.SwappedOutContext.SwitchTo();
        }

        //--//

        //
        // Helper Methods
        //

        private void RegisterThread( ThreadImpl thread )
        {
            BugCheck.AssertInterruptsOff();

            m_allThreads.InsertAtTail( thread.RegistrationLink );
        }

        public virtual void AddThread( ThreadImpl thread )
        {
            BugCheck.Assert( thread.SchedulingLink.VerifyUnlinked(), BugCheck.StopCode.KernelNodeStillLinked );

            using(SmartHandles.InterruptState hnd = SmartHandles.InterruptState.Disable())
            {
                RegisterThread( thread );

                InsertInPriorityOrder( thread );

                RescheduleAndRequestContextSwitchIfNeeded( hnd.GetCurrentExceptionMode() );
            }
        }

        public virtual void RemoveThread( ThreadImpl thread )
        {
            using(SmartHandles.InterruptState hnd = SmartHandles.InterruptState.Disable())
            {
                thread.Detach();

                if(thread == m_runningThread)
                {
                    RescheduleAndRequestContextSwitchIfNeeded( hnd.GetCurrentExceptionMode() );
                }
                else
                {
                    //
                    // If the thread is not the running one, it won't get a chance to execute the Stop method.
                    //
                    thread.Stop();
                }
            }
        }

        public virtual void RetireThread( ThreadImpl thread )
        {
            m_deadThreadsTime.Merge( thread.ActiveTime );
        }

        //--//

        public virtual void Yield()
        {
            BugCheck.AssertInterruptsOn();

            ThreadImpl thisThread = ThreadImpl.CurrentThread;

            BugCheck.Assert( thisThread != null, BugCheck.StopCode.NoCurrentThread );

            InsertInPriorityOrder( thisThread );

            RescheduleAndRequestContextSwitchIfNeeded( HardwareException.None );
        }

        public virtual void SwitchToWait( Synchronization.WaitingRecord wr )
        {
            BugCheck.AssertInterruptsOn();

            using(SmartHandles.InterruptState hnd = SmartHandles.InterruptState.Disable())
            {
                if(wr.Processed == false)
                {
                    ThreadImpl thread = wr.Source;

                    m_waitingThreads.InsertAtTail( thread.SchedulingLink );

                    thread.State |= System.Threading.ThreadState.WaitSleepJoin;

                    InvalidateNextWaitTimer();

                    RescheduleAndRequestContextSwitchIfNeeded( hnd.GetCurrentExceptionMode() );

                    while(thread.IsWaiting)
                    {
                        hnd.Toggle();
                    }
                }
            }
        }

        public virtual void Wakeup( ThreadImpl thread )
        {
            using(SmartHandles.InterruptState hnd = SmartHandles.InterruptState.Disable())
            {
                if(thread.IsWaiting)
                {
                    thread.State &= ~System.Threading.ThreadState.WaitSleepJoin;

                    InsertInPriorityOrder( thread );

                    RescheduleAndRequestContextSwitchIfNeeded( hnd.GetCurrentExceptionMode() );
                }
            }
        }

        public virtual void TimeQuantumExpired()
        {
            BugCheck.AssertInterruptsOff();

            InsertInPriorityOrder( m_runningThread );

            Reschedule();
        }

        public virtual void SetNextQuantumTimerIfNeeded()
        {
            ThreadImpl nextThread = m_nextThread;

            if(nextThread == m_idleThread)
            {
                CancelQuantumTimer(); // No need to set a timer, we are just idling.
            }
            else
            {
                ThreadImpl lastThread = m_readyThreads.LastTarget();

                //
                // If the next thread is not an idle thread, there has to be a ready thread.
                //
                BugCheck.Assert( lastThread != null, BugCheck.StopCode.ExpectingReadyThread );

                if(lastThread == nextThread)
                {
                    CancelQuantumTimer(); // Only one ready thread, no need to preempt it.
                }
                else
                {
                    SetNextQuantumTimer();
                }
            }
        }

        public void RescheduleAndRequestContextSwitchIfNeeded( HardwareException mode )
        {
            Reschedule();

            if(mode == HardwareException.None)
            {
                if(this.ShouldContextSwitch)
                {
                    Peripherals.Instance.CauseInterrupt();
                }
            }
        }

        public virtual void Reschedule()
        {
            SelectNextThreadToRun();
        }

        public void SelectNextThreadToRun()
        {
            using(SmartHandles.InterruptState.Disable())
            {
                ThreadImpl thread = m_readyThreads.FirstTarget();

                m_nextThread = thread != null ? thread : m_idleThread;

                SetNextQuantumTimerIfNeeded();
            }
        }

        public abstract void SetNextWaitTimer( SchedulerTime nextTimeout );

        public abstract void CancelQuantumTimer();

        public abstract void SetNextQuantumTimer();

        public abstract void SetNextQuantumTimer( SchedulerTime nextTimeout );

        public void InvalidateNextWaitTimer()
        {
            if(m_noInvalidateNextWaitTimerRecursion == false)
            {
                ComputeNextTimeout();
            }
        }

        protected void WaitExpired( SchedulerTime currentTime )
        {
            m_noInvalidateNextWaitTimerRecursion = true;

            KernelNode< ThreadImpl > node = m_waitingThreads.StartOfForwardWalk;

            while(node.IsValidForForwardMove)
            {
                KernelNode< ThreadImpl > nodeNext = node.Next;

                node.Target.ProcessWaitExpiration( currentTime );

                node = nodeNext;
            }

            m_noInvalidateNextWaitTimerRecursion = false;

            ComputeNextTimeout();
        }

        private void ComputeNextTimeout()
        {
            SchedulerTime            nextTimeout = SchedulerTime.MaxValue;
            KernelNode< ThreadImpl > node        = m_waitingThreads.StartOfForwardWalk;

            while(node.IsValidForForwardMove)
            {
                SchedulerTime threadTimeout = node.Target.GetFirstTimeout();

                if(nextTimeout > threadTimeout)
                {
                    nextTimeout = threadTimeout;
                }

                node = node.Next;
            }

            SetNextWaitTimer( nextTimeout );
        }

        //--//

        public void Sleep( SchedulerTime schedulerTime )
        {
            m_neverSignaledEvent.WaitOne( schedulerTime, false );
        }

        //--//

        [Inline]
        public static SmartHandles.SwapCurrentThreadUnderInterrupt InstallInterruptThread()
        {
            return ThreadImpl.SwapCurrentThreadUnderInterrupt( ThreadManager.Instance.m_interruptThread );
        }

        [Inline]
        public static SmartHandles.SwapCurrentThreadUnderInterrupt InstallFastInterruptThread()
        {
            return ThreadImpl.SwapCurrentThreadUnderInterrupt( ThreadManager.Instance.m_fastInterruptThread );
        }

        [Inline]
        public static SmartHandles.SwapCurrentThreadUnderInterrupt InstallAbortThread()
        {
            return ThreadImpl.SwapCurrentThreadUnderInterrupt( ThreadManager.Instance.m_abortThread );
        }

        //--//

        private void InsertInPriorityOrder( ThreadImpl thread )
        {
            //
            // Insert in order.
            //
            var node = m_readyThreads.StartOfForwardWalk;
            var pri  = thread.Priority;

            while(node.IsValidForForwardMove)
            {
                if(node.Target.Priority < pri)
                {
                    break;
                }

                node = node.Next;
            }

            thread.SchedulingLink.InsertBefore( node );

            thread.State &= ~System.Threading.ThreadState.WaitSleepJoin;
        }

        private void IdleThread()
        {
            SmartHandles.InterruptState.EnableAll();

            while(true)
            {
                Peripherals.Instance.WaitForInterrupt();
            }
        }

        private void MainThread()
        {
            while(true)
            {
                try
                {
                    Configuration.ExecuteApplication();
                }
                catch
                {
                }

                BugCheck.Raise( BugCheck.StopCode.NoCurrentThread ); 
            }
        }

        //
        // Access Methods
        //

        public static extern ThreadManager Instance
        {
            [SingletonFactory(Fallback=typeof(EmptyManager))]
            [MethodImpl( MethodImplOptions.InternalCall )]
            get;
        }

        public ThreadImpl CurrentThread
        {
            get
            {
                return m_runningThread;
            }

            set
            {
                BugCheck.AssertInterruptsOff();

                ThreadImpl oldValue = m_runningThread;

                if(oldValue != value)
                {
                    oldValue.ReleasedProcessor();

                    m_runningThread = value;

                    value.AcquiredProcessor();

                    SetNextQuantumTimerIfNeeded();
                }
            }
        }

        public ThreadImpl NextThread
        {
            get
            {
                return m_nextThread;
            }
        }

        public bool ShouldContextSwitch
        {
            [Inline]
            get
            {
                return m_runningThread != m_nextThread;
            }
        }

        public KernelNode< ThreadImpl > StartOfForwardWalkThroughAllThreads
        {
            get
            {
                return m_allThreads.StartOfForwardWalk;
            }
        }
    }
}

