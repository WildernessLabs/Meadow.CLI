using System;
using System.Collections.Generic;
using MeadowCLI.Hcom;

namespace Meadow.CLI.DeviceManagement
{
    /// <summary>
    /// TODO: put device enumeration and such stuff here.
    /// </summary>
    public static class DeviceManager
    {
        // TODO: should probably be an ObservableList<>
        public static List<MeadowDevice> AttachedDevices = new List<MeadowDevice>();

        static DeviceManager()
        {
            // TODO: populate the list of attached devices

            // TODO: wire up listeners for device plug and unplug

        }

        private static void Handle_DeviceAdded()
        {
            // add device to AttachedDevices
        }

        private static void Handle_DeviceRemoved()
        {
            // remove device from AttachedDevices
        }

        public static void ToggleNsh(MeadowDevice meadow)
        {
            //TODO: mvoe from MeadowFlashMananger
        }

        public static void EnterDfuMode(MeadowDevice meadow)
        {
            //TODO: mvoe from MeadowFlashMananger
        }

        public static void ChangeTraceLevel(MeadowDevice meadow, uint level)
        {
            //TODO: mvoe from MeadowFlashMananger
        }

        public static void ResetTargetMcu(MeadowDevice meadow)
        {
            //TODO: mvoe from MeadowFlashMananger
        }
    }
}
