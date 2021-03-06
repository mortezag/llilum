// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Class:  Stream
**
**
** Purpose: Abstract base class for all Streams.  Provides
** default implementations of asynchronous reads & writes, in
** terms of the synchronous reads & writes (and vice versa).
**
**
===========================================================*/
using System;
using System.Threading;
using System.Runtime.InteropServices;
//using System.Runtime.Remoting.Messaging;
//using System.Security;
//using System.Security.Permissions;

namespace System.IO
{
    [Serializable()]
    public abstract class Stream : MarshalByRefObject, IDisposable
    {
        public static readonly Stream Null = new NullStream();

        public abstract bool CanRead
        {
            get;
        }

        // If CanSeek is false, Position, Seek, Length, and SetLength should throw.
        public abstract bool CanSeek
        {
            get;
        }

        public virtual bool CanTimeout
        {
            get
            {
                return false;
            }
        }

        public abstract bool CanWrite
        {
            get;
        }

        public abstract long Length
        {
            get;
        }

        public abstract long Position
        {
            get;
            set;
        }

        public virtual int ReadTimeout
        {
            get
            {
#if EXCEPTION_STRINGS
                throw new InvalidOperationException( Environment.GetResourceString( "InvalidOperation_TimeoutsNotSupported" ) );
#else
                throw new InvalidOperationException();
#endif
            }
            set
            {
#if EXCEPTION_STRINGS
                throw new InvalidOperationException( Environment.GetResourceString( "InvalidOperation_TimeoutsNotSupported" ) );
#else
                throw new InvalidOperationException();
#endif
            }
        }

        public virtual int WriteTimeout
        {
            get
            {
#if EXCEPTION_STRINGS
                throw new InvalidOperationException( Environment.GetResourceString( "InvalidOperation_TimeoutsNotSupported" ) );
#else
                throw new InvalidOperationException();
#endif
            }
            set
            {
#if EXCEPTION_STRINGS
                throw new InvalidOperationException( Environment.GetResourceString( "InvalidOperation_TimeoutsNotSupported" ) );
#else
                throw new InvalidOperationException();
#endif
            }
        }

        // Stream used to require that all cleanup logic went into Close(),
        // which was thought up before we invented IDisposable.  However, we
        // need to follow the IDisposable pattern so that users can write 
        // sensible subclasses without needing to inspect all their base 
        // classes, and without worrying about version brittleness, from a
        // base class switching to the Dispose pattern.  We're moving
        // Stream to the Dispose(bool) pattern - that's where all subclasses 
        // should put their cleanup starting in V2.
        public virtual void Close()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        public void Dispose()
        {
            Close();
        }

        protected virtual void Dispose( bool disposing )
        {
////        // Note: Never change this to call other virtual methods on Stream
////        // like Write, since the state on subclasses has already been 
////        // torn down.  This is the last code to run on cleanup for a stream.
////        if((disposing) && (_asyncActiveEvent != null))
////            _CloseAsyncActiveEvent( Interlocked.Decrement( ref _asyncActiveCount ) );
        }

////    private void _CloseAsyncActiveEvent( int asyncActiveCount )
////    {
////        BCLDebug.Assert( _asyncActiveCount >= 0, "ref counting mismatch, possible race in the code" );
////
////        // If no pending async IO, close the event
////        if((_asyncActiveEvent != null) && (asyncActiveCount == 0))
////        {
////            _asyncActiveEvent.Close();
////            _asyncActiveEvent = null;
////        }
////    }

        public abstract void Flush();


        [Obsolete( "CreateWaitHandle will be removed eventually.  Please use \"new ManualResetEvent(false)\" instead." )]
        protected virtual WaitHandle CreateWaitHandle()
        {
            return new ManualResetEvent( false );
        }

////    [HostProtection( ExternalThreading = true )]
        public virtual IAsyncResult BeginRead( byte[] buffer, int offset, int count, AsyncCallback callback, Object state )
        {
            if(!CanRead) __Error.ReadNotSupported();

            // To avoid a race with a stream's position pointer & generating race 
            // conditions with internal buffer indexes in our own streams that 
            // don't natively support async IO operations when there are multiple 
            // async requests outstanding, we will block the application's main
            // thread and do the IO synchronously.  
            // This can't perform well - use a different approach.
            SynchronousAsyncResult asyncResult = new SynchronousAsyncResult( state, false );
            try
            {
                int numRead = Read( buffer, offset, count );
                asyncResult._numRead = numRead;
                if(callback != null)
                    callback( asyncResult );
            }
            catch(IOException e)
            {
                asyncResult._exception = e;
            }
            finally
            {
                asyncResult._isCompleted = true;
                asyncResult._waitHandle.Set();
            }

            return asyncResult;
        }

        public virtual int EndRead( IAsyncResult asyncResult )
        {
            if(asyncResult == null)
#if EXCEPTION_STRINGS
                throw new ArgumentNullException( "asyncResult" );
#else
                throw new ArgumentNullException();
#endif

            SynchronousAsyncResult ar = asyncResult as SynchronousAsyncResult;
            if(ar == null || ar.IsWrite)
                __Error.WrongAsyncResult();
            if(ar._EndXxxCalled)
                __Error.EndReadCalledTwice();
            ar._EndXxxCalled = true;

            if(ar._exception != null)
                throw ar._exception;

            return ar._numRead;
        }

////    [HostProtection( ExternalThreading = true )]
        public virtual IAsyncResult BeginWrite( byte[] buffer, int offset, int count, AsyncCallback callback, Object state )
        {
            if(!CanWrite) __Error.WriteNotSupported();

            // To avoid a race with a stream's position pointer & generating race 
            // conditions with internal buffer indexes in our own streams that 
            // don't natively support async IO operations when there are multiple 
            // async requests outstanding, we will block the application's main
            // thread and do the IO synchronously.  
            // This can't perform well - use a different approach.
            SynchronousAsyncResult asyncResult = new SynchronousAsyncResult( state, true );
            try
            {
                Write( buffer, offset, count );
                if(callback != null)
                    callback( asyncResult );
            }
            catch(IOException e)
            {
                asyncResult._exception = e;
            }
            finally
            {
                asyncResult._isCompleted = true;
                asyncResult._waitHandle.Set();
            }

            return asyncResult;
        }

        public virtual void EndWrite( IAsyncResult asyncResult )
        {
            if(asyncResult == null)
#if EXCEPTION_STRINGS
                throw new ArgumentNullException( "asyncResult" );
#else
                throw new ArgumentNullException();
#endif

            SynchronousAsyncResult ar = asyncResult as SynchronousAsyncResult;
            if(ar == null || !ar.IsWrite)
                __Error.WrongAsyncResult();
            if(ar._EndXxxCalled)
                __Error.EndWriteCalledTwice();
            ar._EndXxxCalled = true;

            if(ar._exception != null)
                throw ar._exception;
        }

        public abstract long Seek( long offset, SeekOrigin origin );

        public abstract void SetLength( long value );

        public abstract int Read( [In, Out] byte[] buffer, int offset, int count );

        // Reads one byte from the stream by calling Read(byte[], int, int). 
        // Will return an unsigned byte cast to an int or -1 on end of stream.
        // This implementation does not perform well because it allocates a new
        // byte[] each time you call it, and should be overridden by any 
        // subclass that maintains an internal buffer.  Then, it can help perf
        // significantly for people who are reading one byte at a time.
        public virtual int ReadByte()
        {
            byte[] oneByteArray = new byte[1];
            int r = Read( oneByteArray, 0, 1 );
            if(r == 0)
                return -1;
            return oneByteArray[0];
        }

        public abstract void Write( byte[] buffer, int offset, int count );

        // Writes one byte from the stream by calling Write(byte[], int, int).
        // This implementation does not perform well because it allocates a new
        // byte[] each time you call it, and should be overridden by any 
        // subclass that maintains an internal buffer.  Then, it can help perf
        // significantly for people who are writing one byte at a time.
        public virtual void WriteByte( byte value )
        {
            byte[] oneByteArray = new byte[1];
            oneByteArray[0] = value;
            Write( oneByteArray, 0, 1 );
        }

////    [HostProtection( Synchronization = true )]
        public static Stream Synchronized( Stream stream )
        {
            if(stream == null)
#if EXCEPTION_STRINGS
                throw new ArgumentNullException( "stream" );
#else
                throw new ArgumentNullException();
#endif
            if(stream is SyncStream)
                return stream;

            return new SyncStream( stream );
        }

        [Serializable]
        private sealed class NullStream : Stream
        {
            internal NullStream()
            {
            }

            public override bool CanRead
            {
                get
                {
                    return true;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return true;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return true;
                }
            }

            public override long Length
            {
                get { return 0; }
            }

            public override long Position
            {
                get
                {
                    return 0;
                }
                
                set
                {
                }
            }

            // No need to override Close

            public override void Flush()
            {
            }

            public override int Read( [In, Out] byte[] buffer, int offset, int count )
            {
                return 0;
            }

            public override int ReadByte()
            {
                return -1;
            }

            public override void Write( byte[] buffer, int offset, int count )
            {
            }

            public override void WriteByte( byte value )
            {
            }

            public override long Seek( long offset, SeekOrigin origin )
            {
                return 0;
            }

            public override void SetLength( long length )
            {
            }
        }

        // Used as the IAsyncResult object when using asynchronous IO methods
        // on the base Stream class.  Note I'm not using async delegates, so
        // this is necessary.
        private sealed class SynchronousAsyncResult : IAsyncResult
        {
            internal ManualResetEvent _waitHandle;
            internal Object _stateObject;
            internal int _numRead;
            internal bool _isCompleted;
            internal bool _isWrite;
            internal bool _EndXxxCalled;
            internal Exception _exception;

            internal SynchronousAsyncResult( Object asyncStateObject, bool isWrite )
            {
                _stateObject = asyncStateObject;
                _isWrite = isWrite;
                _waitHandle = new ManualResetEvent( false );
            }

            public bool IsCompleted
            {
                get { return _isCompleted; }
            }

            public WaitHandle AsyncWaitHandle
            {
                get { return _waitHandle; }
            }

            public Object AsyncState
            {
                get { return _stateObject; }
            }

            public bool CompletedSynchronously
            {
                get { return true; }
            }

            internal bool IsWrite
            {
                get { return _isWrite; }
            }
        }

        // SyncStream is a wrapper around a stream that takes 
        // a lock for every operation making it thread safe.

        [Serializable()]
        internal sealed class SyncStream : Stream, IDisposable
        {
            private Stream _stream;

            internal SyncStream( Stream stream )
            {
                if(stream == null)
#if EXCEPTION_STRINGS
                    throw new ArgumentNullException( "stream" );
#else
                    throw new ArgumentNullException();
#endif
                _stream = stream;
            }

            public override bool CanRead
            {
                get
                {
                    return _stream.CanRead;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return _stream.CanWrite;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return _stream.CanSeek;
                }
            }

            public override bool CanTimeout
            {
                get
                {
                    return _stream.CanTimeout;
                }
            }

            public override long Length
            {
                get
                {
                    lock(_stream)
                    {
                        return _stream.Length;
                    }
                }
            }

            public override long Position
            {
                get
                {
                    lock(_stream)
                    {
                        return _stream.Position;
                    }
                }

                set
                {
                    lock(_stream)
                    {
                        _stream.Position = value;
                    }
                }
            }

            public override int ReadTimeout
            {
                get
                {
                    return _stream.ReadTimeout;
                }

                set
                {
                    _stream.ReadTimeout = value;
                }
            }

            public override int WriteTimeout
            {
                get
                {
                    return _stream.WriteTimeout;
                }

                set
                {
                    _stream.WriteTimeout = value;
                }
            }

            // In the off chance that some wrapped stream has different 
            // semantics for Close vs. Dispose, let's preserve that.
            public override void Close()
            {
                lock(_stream)
                {
                    try
                    {
                        _stream.Close();
                    }
                    finally
                    {
                        base.Dispose( true );
                    }
                }
            }

            protected override void Dispose( bool disposing )
            {
                lock(_stream)
                {
                    try
                    {
                        // Explicitly pick up a potentially methodimpl'ed Dispose
                        if(disposing)
                        {
                            ((IDisposable)_stream).Dispose();
                        }
                    }
                    finally
                    {
                        base.Dispose( disposing );
                    }
                }
            }

            public override void Flush()
            {
                lock(_stream)
                {
                    _stream.Flush();
                }
            }

            public override int Read( [In, Out]byte[] bytes, int offset, int count )
            {
                lock(_stream)
                {
                    return _stream.Read( bytes, offset, count );
                }
            }

            public override int ReadByte()
            {
                lock(_stream)
                {
                    return _stream.ReadByte();
                }
            }

////        [HostProtection( ExternalThreading = true )]
            public override IAsyncResult BeginRead( byte[] buffer, int offset, int count, AsyncCallback callback, Object state )
            {
                lock(_stream)
                {
                    return _stream.BeginRead( buffer, offset, count, callback, state );
                }
            }

            public override int EndRead( IAsyncResult asyncResult )
            {
                lock(_stream)
                {
                    return _stream.EndRead( asyncResult );
                }
            }

            public override long Seek( long offset, SeekOrigin origin )
            {
                lock(_stream)
                {
                    return _stream.Seek( offset, origin );
                }
            }

            public override void SetLength( long length )
            {
                lock(_stream)
                {
                    _stream.SetLength( length );
                }
            }

            public override void Write( byte[] bytes, int offset, int count )
            {
                lock(_stream)
                {
                    _stream.Write( bytes, offset, count );
                }
            }

            public override void WriteByte( byte b )
            {
                lock(_stream)
                {
                    _stream.WriteByte( b );
                }
            }

////        [HostProtection( ExternalThreading = true )]
            public override IAsyncResult BeginWrite( byte[] buffer, int offset, int count, AsyncCallback callback, Object state )
            {
                lock(_stream)
                {
                    return _stream.BeginWrite( buffer, offset, count, callback, state );
                }
            }

            public override void EndWrite( IAsyncResult asyncResult )
            {
                lock(_stream)
                {
                    _stream.EndWrite( asyncResult );
                }
            }
        }
    }
}
