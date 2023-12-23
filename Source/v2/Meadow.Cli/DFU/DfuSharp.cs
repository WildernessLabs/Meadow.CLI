using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace DfuSharp
{
    enum Consts
    {
        USB_DT_DFU = 0x21
    }

    public enum LogLevel
    {
        None = 0,
        Error,
        Warning,
        Info,
        Debug
    }

    public delegate void HotplugCallback(IntPtr ctx, IntPtr device, HotplugEventType eventType, IntPtr userData);


    class NativeMethods
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

    [Flags]
    public enum HotplugEventType : uint
    {
        /** A device has been plugged in and is ready to use */
        //LIBUSB_HOTPLUG_EVENT_DEVICE_ARRIVED
        DeviceArrived = 0x01,

        /** A device has left and is no longer available.
         * It is the user's responsibility to call libusb_close on any handle associated with a disconnected device.
         * It is safe to call libusb_get_device_descriptor on a device that has left */
        //LIBUSB_HOTPLUG_EVENT_DEVICE_LEFT
        DeviceLeft = 0x02
    }

    [Flags]
    public enum HotplugFlags : uint
    {
        /** Default value when not using any flags. */
        //LIBUSB_HOTPLUG_NO_FLAGS = 0,
        DefaultNoFlags = 0,

        /** Arm the callback and fire it for all matching currently attached devices. */
        //LIBUSB_HOTPLUG_ENUMERATE
        EnumerateNow = 1 << 0,
    }

    [Flags]
    public enum Capabilities : uint
    {
        /** The libusb_has_capability() API is available. */
        //LIBUSB_CAP_HAS_CAPABILITY
        HasCapabilityAPI = 0x0000,
        /** Hotplug support is available on this platform. */
        //LIBUSB_CAP_HAS_HOTPLUG
        SupportsHotplug = 0x0001,
        /** The library can access HID devices without requiring user intervention.
		 * Note that before being able to actually access an HID device, you may
		 * still have to call additional libusb functions such as
		 * \ref libusb_detach_kernel_driver(). */
        //LIBUSB_CAP_HAS_HID_ACCESS
        SupportsHidDevices = 0x0100,
        /** The library supports detaching of the default USB driver, using 
		 * \ref libusb_detach_kernel_driver(), if one is set by the OS kernel */
        //LIBUSB_CAP_SUPPORTS_DETACH_KERNEL_DRIVER
        SupportsKernalDriverDetaching = 0x0101
    }

    public enum ErrorCodes : int
    {
        /** Success (no error) */
        Success = 0,

        /** Input/output error */
        IOError = -1,

        /** Invalid parameter */
        InvalidParamter = -2,

        /** Access denied (insufficient permissions) */
        AccessDenied = -3,

        /** No such device (it may have been disconnected) */
        NoSuchDevice = -4,

        /** Entity not found */
        EntityNotFound = -5,

        /** Resource busy */
        ResourceBusy = -6,

        /** Operation timed out */
        OperationTimedout = -7,

        /** Overflow */
        Overflow = -8,

        /** Pipe error */
        PipeError = -9,

        /** System call interrupted (perhaps due to signal) */
        SystemCallInterrupted = -10,

        /** Insufficient memory */
        InsufficientMemory = -11,

        /** Operation not supported or unimplemented on this platform */
        OperationNotSupported = -12,

        /* NB: Remember to update LIBUSB_ERROR_COUNT below as well as the
           message strings in strerror.c when adding new error codes here. */

        /** Other error */
        OtherError = -99,
    };

    struct DeviceDescriptor
    {
        public byte bLength;
        public byte bDescriptorType;
        public ushort bcdUSB;
        public byte bDeviceClass;
        public byte bDeviceSubClass;
        public byte bDeviceProtocol;
        public byte bMaxPacketSize0;
        public ushort idVendor;
        public ushort idProduct;
        public ushort bcdDevice;
        public byte iManufacturer;
        public byte iProduct;
        public byte iSerialNumber;
        public byte bNumConfigurations;
    }

    struct ConfigDescriptor
    {
        public byte bLength;
        public byte bDescriptorType;
        public ushort wTotalLength;
        public byte bNumInterfaces;
        public byte bConfigurationValue;
        public byte iConfiguration;
        public byte bmAttributes;
        public byte MaxPower;
        public IntPtr interfaces;
        public IntPtr extra;
        public int extra_length;
    }

    struct @Interface
    {
        public IntPtr altsetting;
        public int num_altsetting;

        public InterfaceDescriptor[] Altsetting
        {
            get
            {
                var descriptors = new InterfaceDescriptor[num_altsetting];
                for (int i = 0; i < num_altsetting; i++)
                {
                    descriptors[i] = Marshal.PtrToStructure<InterfaceDescriptor>(altsetting + i * Marshal.SizeOf<InterfaceDescriptor>());
                }

                return descriptors;
            }
        }
    }

    public struct InterfaceDescriptor
    {
        public byte bLength;
        public byte bDescriptorType;
        public byte bInterfaceNumber;
        public byte bAlternateSetting;
        public byte bNumEndpoints;
        public byte bInterfaceClass;
        public byte bInterfaceSubClass;
        public byte bInterfaceProtocol;
        public byte iInterface;
        public IntPtr endpoint;
        public IntPtr extra;
        public int extra_length;
    }

    public struct DfuFunctionDescriptor
    {
        public byte bLength;
        public byte bDescriptorType;
        public byte bmAttributes;
        public ushort wDetachTimeOut;
        public ushort wTransferSize;
        public ushort bcdDFUVersion;
    }

    public delegate void UploadingEventHandler(object sender, UploadingEventArgs e);

    public class UploadingEventArgs : EventArgs
    {
        public int BytesUploaded { get; private set; }

        public UploadingEventArgs(int bytesUploaded)
        {
            this.BytesUploaded = bytesUploaded;
        }
    }

    public class DfuDevice : IDisposable
    {
        // FIXME: Figure out why dfu_function_descriptor.wTransferSize isn't right and why STM isn't reporting flash_size right
        const int flash_size = 0x200000;
        const int transfer_size = 0x800;
        const int address = 0x08000000;

        IntPtr handle;
        InterfaceDescriptor interface_descriptor;
        DfuFunctionDescriptor dfu_descriptor;

        public DfuDevice(IntPtr device, InterfaceDescriptor interface_descriptor, DfuFunctionDescriptor dfu_descriptor)
        {
            this.interface_descriptor = interface_descriptor;
            this.dfu_descriptor = dfu_descriptor;
            if (NativeMethods.libusb_open(device, ref handle) < 0)
                throw new Exception("Error opening device");
        }

        public event UploadingEventHandler Uploading;

        protected virtual void OnUploading(UploadingEventArgs e)
        {
            if (Uploading != null)
                Uploading(this, e);
        }
        public void ClaimInterface()
        {
            NativeMethods.libusb_claim_interface(handle, interface_descriptor.bInterfaceNumber);
        }

        public void SetInterfaceAltSetting(int alt_setting)
        {
            NativeMethods.libusb_set_interface_alt_setting(handle, interface_descriptor.bInterfaceNumber, alt_setting);
        }

        public void Clear()
        {
            var state = (byte)0xff;

            while (state != 0 && state != 2)
            {
                state = GetStatus(handle, interface_descriptor.bInterfaceNumber);

                switch (state)
                {
                    case 5:
                    case 9:
                        Abort(handle, interface_descriptor.bInterfaceNumber);
                        break;
                    case 10:
                        ClearStatus(handle, interface_descriptor.bInterfaceNumber);
                        break;
                    default:
                        break;
                }
            }
        }

        public void Upload(FileStream file, int? baseAddress = null)
        {
            var buffer = new byte[transfer_size];

            using (var reader = new BinaryReader(file))
            {
                for (var pos = 0; pos < flash_size; pos += transfer_size)
                {
                    int write_address = (baseAddress ?? address) + pos;
                    var count = reader.Read(buffer, 0, transfer_size);

                    if (count == 0)
                        return;

                    Upload(buffer, write_address);
                }
            }
        }

        public void Upload(byte[] data, int? baseAddress = null, int altSetting = 0)
        {
            var mem = Marshal.AllocHGlobal(transfer_size);

            try
            {
                //Clear();
                //ClaimInterface();
                //if (altSetting != 0) SetInterfaceAltSetting(altSetting);

                for (var pos = 0; pos < flash_size; pos += transfer_size)
                {
                    int write_address = (baseAddress ?? address) + pos;
                    var count = Math.Min(data.Length - pos, transfer_size);

                    if (count <= 0)
                        return;

                    Clear();
                    ClaimInterface();
                    if (altSetting != 0) SetInterfaceAltSetting(altSetting);
                    SetAddress(write_address);
                    Clear();

                    Marshal.Copy(data, pos, mem, count);

                    var ret = NativeMethods.libusb_control_transfer(
                                                handle,
                                                0x00 /*LIBUSB_ENDPOINT_OUT*/ | (0x1 << 5) /*LIBUSB_REQUEST_TYPE_CLASS*/ | 0x01 /*LIBUSB_RECIPIENT_INTERFACE*/,
                                                1 /*DFU_DNLOAD*/,
                                                2,
                                                interface_descriptor.bInterfaceNumber,
                                                 mem,
                                                (ushort)count,
                                                5000);

                    if (ret < 0)
                        throw new Exception(string.Format("Error with WRITE_SECTOR: {0}", ret));
                    var status = GetStatus(handle, interface_descriptor.bInterfaceNumber);

                    while (status == 4)
                    {
                        Thread.Sleep(100);
                        status = GetStatus(handle, interface_descriptor.bInterfaceNumber);
                    }
                    OnUploading(new UploadingEventArgs(count));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
            }
        }

        public void Download(FileStream file)
        {
            var buffer = new byte[transfer_size];
            var mem = Marshal.AllocHGlobal(transfer_size);

            try
            {
                int count = 0;
                ushort transaction = 2;
                using (var writer = new BinaryWriter(file))
                {
                    while (count < flash_size)
                    {
                        Clear();
                        ClaimInterface();

                        int ret = NativeMethods.libusb_control_transfer(
                                                                handle,
                                                                0x80 /*LIBUSB_ENDPOINT_IN*/ | (0x1 << 5) /*LIBUSB_REQUEST_TYPE_CLASS*/ | 0x01 /*LIBUSB_RECIPIENT_INTERFACE*/,
                                                                2 /*DFU_UPLOAD*/,
                                                                transaction++,
                                                                interface_descriptor.bInterfaceNumber,
                                                                mem,
                                                                transfer_size,
                                                                5000);
                        if (ret < 0)
                            throw new Exception(string.Format("Error with DFU_UPLOAD: {0}", ret));

                        count += ret;
                        Marshal.Copy(mem, buffer, 0, ret);
                        writer.Write(buffer, 0, ret);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
            }
        }

        public void Download(byte[] block, int address, int altSetting = 0)
        {
            int size = block.Length;

            var mem = Marshal.AllocHGlobal(size);

            try
            {
                ushort transaction = 2;

                Clear();
                ClaimInterface();
                if (altSetting != 0) SetInterfaceAltSetting(altSetting);
                SetAddress(address);
                Clear();

                int ret = NativeMethods.libusb_control_transfer(
                                                        handle,
                                                        0x80 /*LIBUSB_ENDPOINT_IN*/ | (0x1 << 5) /*LIBUSB_REQUEST_TYPE_CLASS*/ | 0x01 /*LIBUSB_RECIPIENT_INTERFACE*/,
                                                        2 /*DFU_UPLOAD*/,
                                                        transaction++,
                                                        interface_descriptor.bInterfaceNumber,
                                                        mem,
                                                        (ushort)size,
                                                        5000);
                if (ret < 0)
                    throw new Exception(string.Format("Error with DFU_UPLOAD: {0}", ret));

                Marshal.Copy(mem, block, 0, ret);
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
                Clear();
            }
        }

        public void EraseSector(int address)
        {
            var mem = Marshal.AllocHGlobal(5);

            try
            {
                Marshal.WriteByte(mem, 0, 0x41);
                Marshal.WriteByte(mem, 1, (byte)((address >> 0) & 0xff));
                Marshal.WriteByte(mem, 2, (byte)((address >> 8) & 0xff));
                Marshal.WriteByte(mem, 3, (byte)((address >> 16) & 0xff));
                Marshal.WriteByte(mem, 4, (byte)((address >> 24) & 0xff));


                var ret = NativeMethods.libusb_control_transfer(
                                        handle,
                                        0x00 /*LIBUSB_ENDPOINT_OUT*/ | (0x1 << 5) /*LIBUSB_REQUEST_TYPE_CLASS*/ | 0x01 /*LIBUSB_RECIPIENT_INTERFACE*/,
                                        1 /*DFU_DNLOAD*/,
                                        0,
                                        interface_descriptor.bInterfaceNumber,
                                        mem,
                                        5,
                                        5000);

                if (ret < 0)
                    throw new Exception(string.Format("Error with ERASE_SECTOR: {0}", ret));

                var status = GetStatus(handle, interface_descriptor.bInterfaceNumber);

                while (status == 4)
                {
                    Thread.Sleep(100);
                    status = GetStatus(handle, interface_descriptor.bInterfaceNumber);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
            }
        }

        public void Reset()
        {
            var mem = Marshal.AllocHGlobal(0);

            try
            {
                var ret = NativeMethods.libusb_control_transfer(
                                        handle,
                                        0x00 /*LIBUSB_ENDPOINT_OUT*/ | (0x1 << 5) /*LIBUSB_REQUEST_TYPE_CLASS*/ | 0x01 /*LIBUSB_RECIPIENT_INTERFACE*/,
                                        1 /*DFU_DNLOAD*/,
                                        0,
                                        interface_descriptor.bInterfaceNumber,
                                        mem,
                                        0,
                                        5000);

                if (ret < 0)
                    throw new Exception(string.Format("Error with RESET: {0}", ret));

                var status = GetStatus(handle, interface_descriptor.bInterfaceNumber);

                while (status == 4)
                {
                    Thread.Sleep(100);
                    status = GetStatus(handle, interface_descriptor.bInterfaceNumber);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
            }
        }

        public void SetAddress(int address)
        {
            var mem = Marshal.AllocHGlobal(5);

            try
            {
                Marshal.WriteByte(mem, 0, 0x21);
                Marshal.WriteByte(mem, 1, (byte)((address >> 0) & 0xff));
                Marshal.WriteByte(mem, 2, (byte)((address >> 8) & 0xff));
                Marshal.WriteByte(mem, 3, (byte)((address >> 16) & 0xff));
                Marshal.WriteByte(mem, 4, (byte)((address >> 24) & 0xff));


                var ret = NativeMethods.libusb_control_transfer(
                                        handle,
                                        0x00 /*LIBUSB_ENDPOINT_OUT*/ | (0x1 << 5) /*LIBUSB_REQUEST_TYPE_CLASS*/ | 0x01 /*LIBUSB_RECIPIENT_INTERFACE*/,
                                        1 /*DFU_DNLOAD*/,
                                        0,
                                        interface_descriptor.bInterfaceNumber,
                                        mem,
                                        5,
                                        5000);

                if (ret < 0)
                    throw new Exception(string.Format("Error with ERASE_SECTOR: {0}", ret));

                var status = GetStatus(handle, interface_descriptor.bInterfaceNumber);

                while (status == 4)
                {
                    Thread.Sleep(100);
                    status = GetStatus(handle, interface_descriptor.bInterfaceNumber);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
            }
        }

        static byte GetStatus(IntPtr dev, ushort interface_number)
        {
            var buffer = Marshal.AllocHGlobal(6);

            try
            {
                int ret = NativeMethods.libusb_control_transfer(
                    dev,
                    0x80 /*LIBUSB_ENDPOINT_IN*/ | (0x1 << 5) /*LIBUSB_REQUEST_TYPE_CLASS*/ | 0x01 /*LIBUSB_RECIPIENT_INTERFACE*/,
                    3 /*DFU_GETSTATUS*/,
                    0,
                    interface_number,
                    buffer,
                    6,
                    5000);

                if (ret == 6)
                    return Marshal.ReadByte(buffer, 4);

                return 0xff;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        static void Abort(IntPtr dev, ushort interface_number)
        {
            int ret = NativeMethods.libusb_control_transfer(
                dev,
                0x00 /*LIBUSB_ENDPOINT_OUT*/ | (0x1 << 5) /*LIBUSB_REQUEST_TYPE_CLASS*/ | 0x01 /*LIBUSB_RECIPIENT_INTERFACE*/,
                6 /*DFU_ABORT*/,
                0,
                interface_number,
                IntPtr.Zero,
                0,
                5000);
        }
        static void ClearStatus(IntPtr dev, ushort interface_number)
        {
            int ret = NativeMethods.libusb_control_transfer(
               dev,
               0x00 /*LIBUSB_ENDPOINT_OUT*/ | (0x1 << 5) /*LIBUSB_REQUEST_TYPE_CLASS*/ | 0x01 /*LIBUSB_RECIPIENT_INTERFACE*/,
               4 /*DFU_GETSTATUS*/,
               0,
               interface_number,
               IntPtr.Zero,
               0,
               5000);
        }
        public void Dispose()
        {
            NativeMethods.libusb_close(handle);
        }
    }

    public class Context : IDisposable
    {
        public event EventHandler DeviceConnected = delegate { };

        // doing this here so its lifecycle is tied to the class
        protected HotplugCallback _hotplugCallbackHandler;

        IntPtr _callbackHandle = IntPtr.Zero;


        IntPtr handle;
        public Context(LogLevel debug_level = LogLevel.None)
        {
            var ret = NativeMethods.libusb_init(ref handle);

            NativeMethods.libusb_set_debug(handle, debug_level);
            if (ret != 0)
                throw new Exception(string.Format("Error: {0} while trying to initialize libusb", ret));

            // instantiate our callback handler
            this._hotplugCallbackHandler = new HotplugCallback(HandleHotplugCallback);
        }

        public void Dispose()
        {
            //this.StopListeningForHotplugEvents(); // not needed, they're automatically deregistered in libusb_exit.
            NativeMethods.libusb_exit(handle);
        }

        public List<DfuDevice> GetDfuDevices(List<ushort> idVendors)
        {
            var list = IntPtr.Zero;
            var dfu_devices = new List<DfuDevice>();
            var ret = NativeMethods.libusb_get_device_list(handle, ref list);

            if (ret < 0)
                throw new Exception(string.Format("Error: {0} while trying to get the device list", ret));

            var devices = new IntPtr[ret];
            Marshal.Copy(list, devices, 0, ret);

            // This is awful nested looping -- we should fix it.
            for (int i = 0; i < ret; i++)
            {
                var device_descriptor = new DeviceDescriptor();
                var ptr = IntPtr.Zero;

                if (NativeMethods.libusb_get_device_descriptor(devices[i], ref device_descriptor) != 0)
                    continue;

                //if (!idVendors.Contains(device_descriptor.idVendor))
                //    continue;

                for (int j = 0; j < device_descriptor.bNumConfigurations; j++)
                {
                    var ret2 = NativeMethods.libusb_get_config_descriptor(devices[i], (ushort)j, out ptr);

                    if (ret2 < 0)
                        continue;
                        //throw new Exception(string.Format("Error: {0} while trying to get the config descriptor", ret2));

                    var config_descriptor = Marshal.PtrToStructure<ConfigDescriptor>(ptr);

                    for (int k = 0; k < config_descriptor.bNumInterfaces; k++)
                    {
                        var p = config_descriptor.interfaces + j * Marshal.SizeOf<@Interface>();

                        if (p == IntPtr.Zero)
                            continue;

                        var @interface = Marshal.PtrToStructure<@Interface>(p);
                        for (int l = 0; l < @interface.num_altsetting; l++)
                        {
                            var interface_descriptor = @interface.Altsetting[l];

                            // Ensure this is a DFU descriptor
                            if (interface_descriptor.bInterfaceClass != 0xfe || interface_descriptor.bInterfaceSubClass != 0x1)
                                continue;

                            var dfu_descriptor = FindDescriptor(interface_descriptor.extra, interface_descriptor.extra_length, (byte)Consts.USB_DT_DFU);
                            if (dfu_descriptor != null)
                                dfu_devices.Add(new DfuDevice(devices[i], interface_descriptor, dfu_descriptor.Value));
                        }
                    }
                }
            }

            NativeMethods.libusb_free_device_list(list, 1);
            return dfu_devices;
        }

        static DfuFunctionDescriptor? FindDescriptor(IntPtr desc_list, int list_len, byte desc_type)
        {
            int p = 0;

            while (p + 1 < list_len)
            {
                int len, type;

                len = Marshal.ReadByte(desc_list, p);
                type = Marshal.ReadByte(desc_list, p + 1);

                if (type == desc_type)
                {
                    return Marshal.PtrToStructure<DfuFunctionDescriptor>(desc_list + p);
                }
                p += len;
            }

            return null;
        }

        public bool HasCapability(Capabilities caps)
        {
            return NativeMethods.libusb_has_capability(caps) == 0 ? false : true;
        }

        public void BeginListeningForHotplugEvents()
        {
            if (_callbackHandle != IntPtr.Zero)
            {
                Debug.WriteLine("Already listening for events.");
                return;
            }

            if (!HasCapability(Capabilities.HasCapabilityAPI))
            {
                Debug.WriteLine("Capability API not supported.");
                return;
            }

            if (!HasCapability(Capabilities.SupportsHotplug))
            {
                Debug.WriteLine("Hotplug notifications not supported.");
                return;
            }

            int vendorID = -1; // wildcard match (all)
            int productID = -1;
            int deviceClass = -1;
            IntPtr userData = IntPtr.Zero;

            ErrorCodes success = NativeMethods.libusb_hotplug_register_callback(this.handle, HotplugEventType.DeviceArrived | HotplugEventType.DeviceLeft, HotplugFlags.DefaultNoFlags,
                                                                    vendorID, productID, deviceClass, this._hotplugCallbackHandler, userData, out _callbackHandle);

            if (success == ErrorCodes.Success)
            {
                Debug.WriteLine("Callback registration successful");
            }
            else
            {
                throw new Exception("callback registration failed, error: " + success.ToString());
            }

        }

        public void StopListeningForHotplugEvents()
        {
            if (_callbackHandle == IntPtr.Zero)
            {
                Debug.WriteLine("Not listening already.");
                return;
            }

            NativeMethods.libusb_hotplug_deregister_callback(this.handle, this._callbackHandle);

        }

        public void HandleHotplugCallback(IntPtr ctx, IntPtr device, HotplugEventType eventType, IntPtr userData)
        {
            Debug.WriteLine("Hotplug Callback called, event type: " + eventType.ToString());
            // raise the event
            this.DeviceConnected(this, new EventArgs());
        }
    }
}