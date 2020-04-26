using System;
using System.Collections.Generic;
using Meadow.CLI.DeviceMonitor;

namespace Meadow.CLI.Internals.Udev
{
    public static class Udev
    {
        
        static public String[] GetUSBDevicePaths(string attribute, string value, string subSystem="tty")
        {
            if (LibudevNative.Instance == null) return null;
            
            var paths = new List<string>();

            IntPtr udev = LibudevNative.Instance.udev_new();
            if (IntPtr.Zero != udev)
            {
                try
                {
                    IntPtr enumerate = LibudevNative.Instance.udev_enumerate_new(udev);
                    if (IntPtr.Zero != enumerate)
                    {
                        try
                        {
                            if (LibudevNative.Instance.udev_enumerate_add_match_subsystem(enumerate, subSystem) == 0 &&
                                LibudevNative.Instance.udev_enumerate_scan_devices(enumerate) == 0)
                            {
                                IntPtr entry;
                                for (entry = LibudevNative.Instance.udev_enumerate_get_list_entry(enumerate); entry != IntPtr.Zero;
                                     entry = LibudevNative.Instance.udev_list_entry_get_next(entry))
                                {
                                    string syspath = LibudevNative.Instance.udev_list_entry_get_name(entry);
                                    IntPtr device = LibudevNative.Instance.udev_device_new_from_syspath(udev, syspath);                                   
                                    
                                    string devnode = LibudevNative.Instance.udev_device_get_devnode(device);
                                    
                                    IntPtr parent = LibudevNative.Instance.udev_device_get_parent_with_subsystem_devtype(device, "usb", "usb_device");
                                    if (IntPtr.Zero != parent)
                                    {
                                        string attributeValue = LibudevNative.Instance.udev_device_get_sysattr_value(parent, attribute);
                                        if (attributeValue == value) paths.Add(devnode);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            LibudevNative.Instance.udev_enumerate_unref(enumerate);
                        }
                    }
                }
                finally
                {
                    LibudevNative.Instance.udev_unref(udev);
                }
            }

                return paths.ToArray();
        }
        
        
        static public String GetUSBDevicePath(int busNumber, int deviceNumber, string subSystem="tty")
        {

            if (LibudevNative.Instance == null) return null;
        
            var paths = new List<string>();
            var busNumerString = busNumber.ToString();
            var deviceNumberString = deviceNumber.ToString();

            IntPtr udev = LibudevNative.Instance.udev_new();
            if (IntPtr.Zero != udev)
            {
                try
                {
                    IntPtr enumerate = LibudevNative.Instance.udev_enumerate_new(udev);
                    if (IntPtr.Zero != enumerate)
                    {
                        try
                        {
                            if (LibudevNative.Instance.udev_enumerate_add_match_subsystem(enumerate, subSystem) == 0 &&
                                LibudevNative.Instance.udev_enumerate_scan_devices(enumerate) == 0)
                            {
                                IntPtr entry;
                                for (entry = LibudevNative.Instance.udev_enumerate_get_list_entry(enumerate); entry != IntPtr.Zero;
                                     entry = LibudevNative.Instance.udev_list_entry_get_next(entry))
                                {
                                    string syspath = LibudevNative.Instance.udev_list_entry_get_name(entry);
                                    IntPtr device = LibudevNative.Instance.udev_device_new_from_syspath(udev, syspath);                                   
                                    
                                    string devnode = LibudevNative.Instance.udev_device_get_devnode(device);
                                    
                                    IntPtr parent = LibudevNative.Instance.udev_device_get_parent_with_subsystem_devtype(device, "usb", "usb_device");
                                    if (IntPtr.Zero != parent)
                                    {
                                        if (LibudevNative.Instance.udev_device_get_sysattr_value(parent, "busnum") == busNumerString &&
                                            LibudevNative.Instance.udev_device_get_sysattr_value(parent, "devnum") == deviceNumberString)
                                        {
                                            return devnode;
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            LibudevNative.Instance.udev_enumerate_unref(enumerate);
                        }
                    }
                }
                finally
                {
                    LibudevNative.Instance.udev_unref(udev);
                }
            }

            return null;
        }


        static public List<string> GetSerialNumbers(string idVendor, string idProduct, string subSystem="usb")
        {

            if (LibudevNative.Instance == null) return null;
        
            var serials = new List<string>();

            IntPtr udev = LibudevNative.Instance.udev_new();
            if (IntPtr.Zero != udev)
            {
                try
                {
                    IntPtr enumerate = LibudevNative.Instance.udev_enumerate_new(udev);
                    if (IntPtr.Zero != enumerate)
                    {
                        try
                        {                        
                            if (LibudevNative.Instance.udev_enumerate_add_match_subsystem(enumerate, subSystem) == 0 &&
                                LibudevNative.Instance.udev_enumerate_scan_devices(enumerate) == 0)
                            {
                                IntPtr entry;
                                for (entry = LibudevNative.Instance.udev_enumerate_get_list_entry(enumerate); entry != IntPtr.Zero;
                                     entry = LibudevNative.Instance.udev_list_entry_get_next(entry))
                                {                                
                                    string syspath = LibudevNative.Instance.udev_list_entry_get_name(entry);
                                    IntPtr device = LibudevNative.Instance.udev_device_new_from_syspath(udev, syspath);                                   
                                    IntPtr parent = LibudevNative.Instance.udev_device_get_parent_with_subsystem_devtype(device, "usb", "usb_device");
                                    if (IntPtr.Zero != parent)
                                    {
                                        if (LibudevNative.Instance.udev_device_get_sysattr_value(parent, "idVendor") == idVendor &&
                                            LibudevNative.Instance.udev_device_get_sysattr_value(parent, "idProduct") == idProduct)
                                        {

                                            var serial = LibudevNative.Instance.udev_device_get_sysattr_value(parent, "serial");
                                            serials.Add(serial);
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            LibudevNative.Instance.udev_enumerate_unref(enumerate);
                        }
                    }
                }
                finally
                {
                    LibudevNative.Instance.udev_unref(udev);
                }
            }

            return serials;
        }


        
        static public Connection GetDeviceFromPath(string devicePath)
        {
            Connection meadowDevice = new Connection();
            if (LibudevNative.Instance == null) throw new Exception("Udev not availabe");
        

            IntPtr udev = LibudevNative.Instance.udev_new();
            if (IntPtr.Zero != udev)
            {
                try
                {
                    IntPtr enumerate = LibudevNative.Instance.udev_enumerate_new(udev);
                    if (IntPtr.Zero != enumerate)
                    {
                        try
                        {
                            if (LibudevNative.Instance.udev_enumerate_add_match_subsystem(enumerate, "tty") == 0 &&
                                LibudevNative.Instance.udev_enumerate_scan_devices(enumerate) == 0)
                            {
                                IntPtr entry;
                                for (entry = LibudevNative.Instance.udev_enumerate_get_list_entry(enumerate); entry != IntPtr.Zero;
                                     entry = LibudevNative.Instance.udev_list_entry_get_next(entry))
                                {
                                    string syspath = LibudevNative.Instance.udev_list_entry_get_name(entry);
                                    IntPtr device = LibudevNative.Instance.udev_device_new_from_syspath(udev, syspath);                                   
                                    
                                    string devnode = LibudevNative.Instance.udev_device_get_devnode(device);

                                    if (devnode == devicePath)
                                    {
                                        IntPtr parent = LibudevNative.Instance.udev_device_get_parent_with_subsystem_devtype(device, "usb", "usb_device");
                                        if (IntPtr.Zero != parent)
                                        {
                                            meadowDevice.USB = new Connection.USB_interface()
                                            {
                                                DevicePort = devicePath,
                                                BusNumber = ushort.Parse(LibudevNative.Instance.udev_device_get_sysattr_value(parent, "busnum")),
                                                DeviceNumber = ushort.Parse(LibudevNative.Instance.udev_device_get_sysattr_value(parent, "devnum")),
                                                VendorID = Convert.ToUInt16(LibudevNative.Instance.udev_device_get_sysattr_value(parent, "idVendor"), 16),
                                                ProductID = Convert.ToUInt16(LibudevNative.Instance.udev_device_get_sysattr_value(parent, "idProduct"), 16)
                                            };
                                            meadowDevice.SerialNumber = LibudevNative.Instance.udev_device_get_sysattr_value(parent, "serial");
                        
                                            return meadowDevice;
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            LibudevNative.Instance.udev_enumerate_unref(enumerate);
                        }
                    }
                }
                finally
                {
                    LibudevNative.Instance.udev_unref(udev);
                }
            }
            return null;
        }

      
        
    }
}
