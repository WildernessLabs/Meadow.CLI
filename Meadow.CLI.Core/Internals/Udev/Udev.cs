using System;
using System.Collections.Generic;

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

        
        
    }
}
