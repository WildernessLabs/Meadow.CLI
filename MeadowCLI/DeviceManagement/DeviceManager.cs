using System.Collections.Generic;
using MeadowCLI.DeviceManagement;
using MeadowCLI.Hcom;
using static Meadow.CLI.DeviceManagement.MeadowFileManager;

namespace Meadow.CLI.DeviceManagement
{
    /// <summary>
    /// TODO: put device enumeration and such stuff here.
    /// </summary>
    public static class DeviceManager
    {
        // TODO: should probably be an ObservableList<>
        public static List<MeadowDevice> AttachedDevices = new List<MeadowDevice>();

        static HcomMeadowRequestType _meadowRequestType;

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

        //providing a numeric (0 = none, 1 = info and 2 = debug)
        public static void ChangeTraceLevel(MeadowDevice meadow, uint level)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_CHANGE_TRACE_LEVEL;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, level);
        }

        public static void ResetTargetMcu(MeadowDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESET_PRIMARY_MCU;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public static void EnterDfuMode(MeadowDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENTER_DFU_MODE;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public static void ToggleNsh(MeadowDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }



        //ToDo - look these up - I assume their developer modes? Should be SetDev1, etc. ?
        public static void Developer1(MeadowDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_1;
            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public static void Developer2(MeadowDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_2;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public static void Developer3(MeadowDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_3;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public static void Developer4(MeadowDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_4;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }
    }
}
