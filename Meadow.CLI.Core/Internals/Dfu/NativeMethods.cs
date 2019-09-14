using System;
using System.Runtime.InteropServices;

namespace DfuSharp
{
    internal interface ILibUsb
    {
        int init(ref IntPtr ctx);
        void exit(IntPtr ctx);
        void set_debug(IntPtr ctx, LogLevel level);
        int get_device_list(IntPtr ctx, ref IntPtr list);
        int free_device_list(IntPtr list, int free_devices);
        int get_device_descriptor(IntPtr dev, ref DeviceDescriptor desc);
        int get_config_descriptor(IntPtr dev, ushort config_index, out IntPtr desc);
        int open(IntPtr dev, ref IntPtr handle);
        int close(IntPtr handle);
        int claim_interface(IntPtr dev, int interface_number);
        int set_interface_alt_setting(IntPtr dev, int interface_number, int alternate_setting);
        int control_transfer(IntPtr dev, byte bmRequestType, byte bRequest, ushort wValue, ushort wIndex, IntPtr data, ushort wLength, uint timeout);
        int has_capability(Capabilities capability);
        ErrorCodes hotplug_register_callback(IntPtr ctx, HotplugEventType eventType, HotplugFlags flags,
                                                                    int vendorID, int productID, int deviceClass,
                                                                    HotplugCallback callback, IntPtr userData,
                                                                    out IntPtr callbackHandle);
        void hotplug_deregister_callback(IntPtr ctx, IntPtr callbackHandle);
    }

    internal static class LibUsb
    {
        private static ILibUsb _libUsb;

        static LibUsb()
        {
            switch (Environment.OSVersion.Platform) {
                case PlatformID.MacOSX:
                case PlatformID.Unix:
                    _libUsb = new LibUsbMac();
                    break;
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                    _libUsb = new LibUsbWindows();
                    break;
            }
        }

        public static int libusb_init(ref IntPtr ctx)
        {
            return _libUsb.init(ref ctx);
        }

        public static void libusb_exit(IntPtr ctx)
        {
            _libUsb.exit(ctx);
        }

        public static void libusb_set_debug(IntPtr ctx, LogLevel level)
        {
            _libUsb.set_debug(ctx, level);
        }

        public static int libusb_get_device_list(IntPtr ctx, ref IntPtr list)
        {
            return _libUsb.get_device_list(ctx, ref list);
        }

        public static int libusb_free_device_list(IntPtr list, int free_devices)
        {
            return _libUsb.free_device_list(list, free_devices);
        }

        public static int libusb_get_device_descriptor(IntPtr dev, ref DeviceDescriptor desc)
        {
            return _libUsb.get_device_descriptor(dev, ref desc);
        }

        public static int libusb_get_config_descriptor(IntPtr dev, ushort config_index, out IntPtr desc)
        {
            return _libUsb.get_config_descriptor(dev, config_index, out desc);
        }

        public static int libusb_open(IntPtr dev, ref IntPtr handle)
        {
            return _libUsb.open(dev, ref handle);
        }

        public static int libusb_close(IntPtr handle)
        {
            return _libUsb.close(handle);
        }

        public static int libusb_claim_interface(IntPtr dev, int interface_number)
        {
            return _libUsb.claim_interface(dev, interface_number);
        }

        public static int libusb_set_interface_alt_setting(IntPtr dev, int interface_number, int alternate_setting)
        {
            return _libUsb.set_interface_alt_setting(dev, interface_number, alternate_setting);
        }

        public static int libusb_control_transfer(IntPtr dev, byte bmRequestType, byte bRequest, ushort wValue, ushort wIndex, IntPtr data, ushort wLength, uint timeout)
        {
            return _libUsb.control_transfer(dev, bmRequestType, bRequest, wValue, wIndex, data, wLength, timeout);
        }

        public static int libusb_has_capability(Capabilities capability)
        {
            return _libUsb.has_capability(capability);
        }

        public static ErrorCodes libusb_hotplug_register_callback(IntPtr ctx, HotplugEventType eventType, HotplugFlags flags,
                                                                    int vendorID, int productID, int deviceClass,
                                                                    HotplugCallback callback, IntPtr userData,
                                                                    out IntPtr callbackHandle)
        {
            return _libUsb.hotplug_register_callback(ctx, eventType, flags, vendorID, productID, deviceClass, callback, userData, out callbackHandle);
        }

        public static void libusb_hotplug_deregister_callback(IntPtr ctx, IntPtr callbackHandle)
        {
            _libUsb.hotplug_deregister_callback(ctx, callbackHandle);
        }
    }

    internal class LibUsbWindows : ILibUsb
    {
        public int init(ref IntPtr ctx)
        {
            return NativeMethods.libusb_init(ref ctx);
        }

        public void exit(IntPtr ctx)
        {
            NativeMethods.libusb_exit(ctx);
        }

        public void set_debug(IntPtr ctx, LogLevel level)
        {
            NativeMethods.libusb_set_debug(ctx, level);
        }

        public int get_device_list(IntPtr ctx, ref IntPtr list)
        {
            return NativeMethods.libusb_get_device_list(ctx, ref list);
        }

        public int free_device_list(IntPtr list, int free_devices)
        {
            return NativeMethods.libusb_free_device_list(list, free_devices);
        }

        public int get_device_descriptor(IntPtr dev, ref DeviceDescriptor desc)
        {
            return NativeMethods.libusb_get_device_descriptor(dev, ref desc);
        }

        public int get_config_descriptor(IntPtr dev, ushort config_index, out IntPtr desc)
        {
            return NativeMethods.libusb_get_config_descriptor(dev, config_index, out desc);
        }

        public int open(IntPtr dev, ref IntPtr handle)
        {
            return NativeMethods.libusb_open(dev, ref handle);
        }

        public int close(IntPtr handle)
        {
            return NativeMethods.libusb_close(handle);
        }

        public int claim_interface(IntPtr dev, int interface_number)
        {
            return NativeMethods.libusb_claim_interface(dev, interface_number);
        }

        public int set_interface_alt_setting(IntPtr dev, int interface_number, int alternate_setting)
        {
            return NativeMethods.libusb_set_interface_alt_setting(dev, interface_number, alternate_setting);
        }

        public int control_transfer(IntPtr dev, byte bmRequestType, byte bRequest, ushort wValue, ushort wIndex, IntPtr data, ushort wLength, uint timeout)
        {
            return NativeMethods.libusb_control_transfer(dev, bmRequestType, bmRequestType, wValue, wIndex, data, wLength, timeout);
        }

        public int has_capability(Capabilities capability)
        {
            return NativeMethods.libusb_has_capability(capability);
        }

        public ErrorCodes hotplug_register_callback(IntPtr ctx, HotplugEventType eventType, HotplugFlags flags,
                                                                    int vendorID, int productID, int deviceClass,
                                                                    HotplugCallback callback, IntPtr userData,
                                                                    out IntPtr callbackHandle)
        {
            return NativeMethods.libusb_hotplug_register_callback(ctx, eventType, flags, vendorID, productID, deviceClass, callback, userData, out callbackHandle);
        }

        public void hotplug_deregister_callback(IntPtr ctx, IntPtr callbackHandle)
        {
            NativeMethods.libusb_hotplug_deregister_callback(ctx, callbackHandle);
        }

        private static partial class NativeMethods
        {
            const string LIBUSB_LIBRARY = "libusb-1.0.dll";

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_init(ref IntPtr ctx);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern void libusb_exit(IntPtr ctx);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern void libusb_set_debug(IntPtr ctx, LogLevel level);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_get_device_list(IntPtr ctx, ref IntPtr list);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_free_device_list(IntPtr list, int free_devices);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_get_device_descriptor(IntPtr dev, ref DeviceDescriptor desc);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_get_config_descriptor(IntPtr dev, ushort config_index, out IntPtr desc);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_open(IntPtr dev, ref IntPtr handle);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_close(IntPtr handle);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_claim_interface(IntPtr dev, int interface_number);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_set_interface_alt_setting(IntPtr dev, int interface_number, int alternate_setting);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_control_transfer(IntPtr dev, byte bmRequestType, byte bRequest, ushort wValue, ushort wIndex, IntPtr data, ushort wLength, uint timeout);

            /// <summary>
            /// Whether or not the USB supports a particular feature.
            /// </summary>
            /// <returns>nonzero if the running library has the capability, 0 otherwise</returns>
            /// <param name="capability">Capability.</param>
            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_has_capability(Capabilities capability);


            [DllImport(LIBUSB_LIBRARY)]
            internal static extern ErrorCodes libusb_hotplug_register_callback(IntPtr ctx, HotplugEventType eventType, HotplugFlags flags,
                                                                        int vendorID, int productID, int deviceClass,
                                                                        HotplugCallback callback, IntPtr userData,
                                                                        out IntPtr callbackHandle);
            [DllImport(LIBUSB_LIBRARY)]
            internal static extern void libusb_hotplug_deregister_callback(IntPtr ctx, IntPtr callbackHandle);

        }
    }

    internal class LibUsbMac : ILibUsb
    {
        public int init(ref IntPtr ctx)
        {
            return NativeMethods.libusb_init(ref ctx);
        }

        public void exit(IntPtr ctx)
        {
            NativeMethods.libusb_exit(ctx);
        }

        public void set_debug(IntPtr ctx, LogLevel level)
        {
            NativeMethods.libusb_set_debug(ctx, level);
        }

        public int get_device_list(IntPtr ctx, ref IntPtr list)
        {
            return NativeMethods.libusb_get_device_list(ctx, ref list);
        }

        public int free_device_list(IntPtr list, int free_devices)
        {
            return NativeMethods.libusb_free_device_list(list, free_devices);
        }

        public int get_device_descriptor(IntPtr dev, ref DeviceDescriptor desc)
        {
            return NativeMethods.libusb_get_device_descriptor(dev, ref desc);
        }

        public int get_config_descriptor(IntPtr dev, ushort config_index, out IntPtr desc)
        {
            return NativeMethods.libusb_get_config_descriptor(dev, config_index, out desc);
        }

        public int open(IntPtr dev, ref IntPtr handle)
        {
            return NativeMethods.libusb_open(dev, ref handle);
        }

        public int close(IntPtr handle)
        {
            return NativeMethods.libusb_close(handle);
        }

        public int claim_interface(IntPtr dev, int interface_number)
        {
            return NativeMethods.libusb_claim_interface(dev, interface_number);
        }

        public int set_interface_alt_setting(IntPtr dev, int interface_number, int alternate_setting)
        {
            return NativeMethods.libusb_set_interface_alt_setting(dev, interface_number, alternate_setting);
        }

        public int control_transfer(IntPtr dev, byte bmRequestType, byte bRequest, ushort wValue, ushort wIndex, IntPtr data, ushort wLength, uint timeout)
        {
            return NativeMethods.libusb_control_transfer(dev, bmRequestType, bmRequestType, wValue, wIndex, data, wLength, timeout);
        }

        public int has_capability(Capabilities capability)
        {
            return NativeMethods.libusb_has_capability(capability);
        }

        public ErrorCodes hotplug_register_callback(IntPtr ctx, HotplugEventType eventType, HotplugFlags flags,
                                                                    int vendorID, int productID, int deviceClass,
                                                                    HotplugCallback callback, IntPtr userData,
                                                                    out IntPtr callbackHandle)
        {
            return NativeMethods.libusb_hotplug_register_callback(ctx, eventType, flags, vendorID, productID, deviceClass, callback, userData, out callbackHandle);
        }

        public void hotplug_deregister_callback(IntPtr ctx, IntPtr callbackHandle)
        {
            NativeMethods.libusb_hotplug_deregister_callback(ctx, callbackHandle);
        }

        private static partial class NativeMethods
        {
            const string LIBUSB_LIBRARY = "libusb-1.0";

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_init(ref IntPtr ctx);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern void libusb_exit(IntPtr ctx);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern void libusb_set_debug(IntPtr ctx, LogLevel level);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_get_device_list(IntPtr ctx, ref IntPtr list);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_free_device_list(IntPtr list, int free_devices);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_get_device_descriptor(IntPtr dev, ref DeviceDescriptor desc);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_get_config_descriptor(IntPtr dev, ushort config_index, out IntPtr desc);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_open(IntPtr dev, ref IntPtr handle);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_close(IntPtr handle);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_claim_interface(IntPtr dev, int interface_number);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_set_interface_alt_setting(IntPtr dev, int interface_number, int alternate_setting);

            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_control_transfer(IntPtr dev, byte bmRequestType, byte bRequest, ushort wValue, ushort wIndex, IntPtr data, ushort wLength, uint timeout);

            /// <summary>
            /// Whether or not the USB supports a particular feature.
            /// </summary>
            /// <returns>nonzero if the running library has the capability, 0 otherwise</returns>
            /// <param name="capability">Capability.</param>
            [DllImport(LIBUSB_LIBRARY)]
            internal static extern int libusb_has_capability(Capabilities capability);


            [DllImport(LIBUSB_LIBRARY)]
            internal static extern ErrorCodes libusb_hotplug_register_callback(IntPtr ctx, HotplugEventType eventType, HotplugFlags flags,
                                                                        int vendorID, int productID, int deviceClass,
                                                                        HotplugCallback callback, IntPtr userData,
                                                                        out IntPtr callbackHandle);
            [DllImport(LIBUSB_LIBRARY)]
            internal static extern void libusb_hotplug_deregister_callback(IntPtr ctx, IntPtr callbackHandle);

        }
    }
}