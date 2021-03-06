using System;
using System.Runtime.CompilerServices;
using Windows.Devices.Gpio.Provider;

namespace Windows.Devices.Gpio
{
    /// <summary>
    /// Represents the default general-purpose I/O (GPIO) controller for the system.
    /// </summary>
    public sealed class GpioController
    {
        private readonly IGpioControllerProvider _provider;

        internal GpioController()
        {
        }

        public GpioController(IGpioControllerProvider provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// Gets the number of pins on the general-purpose I/O (GPIO) controller.
        /// </summary>
        /// <value>The number of pins on the GPIO controller. Some pins may not be available in user mode. For
        ///     information about how the pin numbers correspond to physical pins, see the documentation for your
        ///     circuit board.</value>
        public int PinCount => _provider.PinCount;

        /// <summary>
        /// Gets the default general-purpose I/O (GPIO) controller for the system.
        /// </summary>
        /// <returns>The default GPIO controller for the system, or null if the system has no GPIO controller.</returns>
        public static GpioController GetDefault()
        {
            return null;
        }

        /// <summary>
        /// Opens a connection to the specified general-purpose I/O (GPIO) pin in exclusive mode.
        /// </summary>
        /// <param name="pinNumber">The pin number of the GPIO pin that you want to open. Some pins may not be available
        ///     in user mode. For information about how the pin numbers correspond to physical pins, see the
        ///     documentation for your circuit board.</param>
        /// <returns>The opened GPIO pin.</returns>
        public GpioPin OpenPin(int pinNumber)
        {
            return OpenPin(pinNumber, GpioSharingMode.Exclusive);
        }

        /// <summary>
        /// Opens the specified general-purpose I/O (GPIO) pin in the specified mode.
        /// </summary>
        /// <param name="pinNumber">The pin number of the GPIO pin that you want to open. Some pins may not be available
        ///     in user mode. For information about how the pin numbers correspond to physical pins, see the
        ///     documentation for your circuit board.</param>
        /// <param name="sharingMode">The mode in which you want to open the GPIO pin, which determines whether other
        ///     connections to the pin can be opened while you have the pin open.</param>
        /// <returns>The opened GPIO pin.</returns>
        public GpioPin OpenPin(int pinNumber, GpioSharingMode sharingMode)
        {
            GpioPin pin = new GpioPin(_provider);
            if (pin.Reserve(pinNumber) != GpioOpenStatus.PinOpened)
            {
                throw new InvalidOperationException();
            }

            return pin;
        }

        /// <summary>
        /// Opens the specified general-purpose I/O (GPIO) pin in the specified mode, and gets a status value that can
        /// be used to handle a failure to open the pin programmatically.
        /// </summary>
        /// <param name="pinNumber">The pin number of the GPIO pin that you want to open. Some pins may not be available
        ///     in user mode. For information about how the pin numbers correspond to physical pins, see the
        ///     documentation for your circuit board.</param>
        /// <param name="sharingMode">The mode in which you want to open the GPIO pin, which determines whether other
        ///     connections to the pin can be opened while you have the pin open.</param>
        /// <param name="pin">The opened GPIO pin if the open status is GpioOpenStatus.Success; otherwise null.</param>
        /// <param name="openStatus">An enumeration value that indicates either that the attempt to open the GPIO pin
        ///     succeeded, or the reason that the attempt to open the GPIO pin failed.</param>
        /// <returns>True if the pin could be opened; otherwise false.</returns>
        public bool TryOpenPin(int pinNumber, GpioSharingMode sharingMode, out GpioPin pin, out GpioOpenStatus openStatus)
        {
            GpioPin newPin = new GpioPin(_provider);

            openStatus = newPin.Reserve(pinNumber);
            if (openStatus != GpioOpenStatus.PinOpened)
            {
                newPin.Dispose();
                pin = null;
                return false;
            }

            pin = newPin;
            return true;
        }
    }
}
