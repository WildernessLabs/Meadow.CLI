using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using Meadow.CLI.Internals.MeadowComms.RecvClasses;
using MeadowCLI.Hcom;
using static MeadowCLI.DeviceManagement.MeadowFileManager;

namespace MeadowCLI.DeviceManagement
{
    /// <summary>
    /// TODO: put device enumeration and such stuff here.
    /// </summary>
    public static class MeadowDeviceManager
    {
        public const int HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH = 12;
        public const int HCOM_PROTOCOL_COMMAND_SEQ_NUMBER = 0;
        public const UInt16 HCOM_PROTOCOL_CURRENT_VERSION_NUMBER = 0x0004;
        public const UInt16 HCOM_PROTOCOL_CONTROL_VALUE_DEFAULT = 0x0000;
        public const UInt16 DefaultVS2019DebugPort = 4024;  // Port used by VS 2019

        // Note: While not truly important, it can be noted that size of the s25fl QSPI flash
        // chip's "Page" (i.e. the smallest size it can program) is 256 bytes. By making the
        // maxmimum data block size an even multiple of 256 we insure that each packet received
        // can be immediately written to the s25fl QSPI flash chip.
        public const int maxAllowableDataBlock = 512;
        public const int maxSizeOfXmitPacket = (maxAllowableDataBlock + 4) + (maxAllowableDataBlock / 254);

        //    public static ObservableCollection<MeadowDevice> AttachedDevices = new ObservableCollection<MeadowDevice>();

        public static MeadowSerialDevice CurrentDevice { get; set; } //short cut for now but may be useful

        static HcomMeadowRequestType _meadowRequestType;
        static DebuggingServer debuggingServer;

        static MeadowDeviceManager()
        {
            // TODO: populate the list of attached devices

            // TODO: wire up listeners for device plug and unplug
        }

        //returns null if we can't detect a Meadow board
        public static async Task<MeadowSerialDevice> GetMeadowForSerialPort (string serialPort) //, bool verbose = true)
        {
            var meadow = CurrentDevice = new MeadowSerialDevice(serialPort);

            try
            {
                meadow.Initialize(true);
                var isMeadow = await meadow.SetDeviceInfo();

                if (isMeadow)
                {
                    return meadow;
                }
   
                meadow.SerialPort.Close();
                return null;
            }
            catch //(Exception ex)
            {
                //swallow for now
                return null;
            }
        }

        //we'll move this soon
        public static List<string> FindSerialDevices()
        {
            var devices = new List<string>();

            foreach (var s in SerialPort.GetPortNames())
            {
                //limit Mac searches to tty.usb*, Windows, try all COM ports
                //on Mac it's pretty quick to test em all so we could remove this check 
                if (Environment.OSVersion.Platform != PlatformID.Unix ||
                    s.Contains("tty.usb"))
                {
                    devices.Add(s);
                }
            }
            return devices;
        }

        //providing a numeric (0 = none, 1 = info and 2 = debug)
        public static void SetTraceLevel(MeadowSerialDevice meadow, int level)
        {
            if (level < 1 || level > 4)
                throw new System.ArgumentOutOfRangeException(nameof(level), "Trace level must be between 0 & 3 inclusive");


            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_CHANGE_TRACE_LEVEL;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, (uint)level);
        }

        public static void ResetMeadow(MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESET_PRIMARY_MCU;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }

        public static void EnterDfuMode(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENTER_DFU_MODE;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public static void NshEnable(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, (uint) 1);
        }

        public static void MonoDisable(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_DISABLE;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public static void MonoEnable(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_ENABLE;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public static void MonoRunState(MeadowSerialDevice meadow)
        {
             _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_RUN_STATE;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public static void GetDeviceInfo(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_DEVICE_INFORMATION;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public static void SetDeveloper1(MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_1;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }
        public static void SetDeveloper2(MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_2;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }
        public static void SetDeveloper3(MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_3;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }

        public static void SetDeveloper4(MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_4;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }

        public static void DiagDisable(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_NO_DIAG_TO_HOST;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public static void DiagEnable(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_SEND_SYSLOG_TO_HOST;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public static void RenewFileSys(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_PART_RENEW_FILE_SYS;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public static void QspiWrite(MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_WRITE;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }

        public static void QspiRead(MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_READ;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }

        public static void QspiInit(MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_INIT;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }

        // This method is called to sent to Visual Studio debugging to Mono
        public static void ForwardVisualStudioDataToMono(byte[] debuggingData, MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEBUGGER_MSG;

            new SendTargetData(meadow.SerialPort).BuildAndSendSimpleData(debuggingData, _meadowRequestType, (uint)userData);
        }

        // This method is called to forward from mono debugging to Visual Studio
        public static void ForwardMonoDataToVisualStudio(byte[] debuggerData)
        {
            debuggingServer.SendToVisualStudio(debuggerData);
        }

        // Enter VSDebugging mode.
        public static void VSDebugging(int vsDebugPort)
        {
            // Create an instance of the TCP socket send/receiver class and
            // starts it receiving.
            if (vsDebugPort == 0)
            {
                Console.WriteLine($"With '--VSDebugPort' not found. Assuming Visual Studio 2019 with port {DefaultVS2019DebugPort}");
                vsDebugPort = DefaultVS2019DebugPort;
            }

            debuggingServer = new DebuggingServer(vsDebugPort);
            debuggingServer.StartListening();
        }

        public static void EnterEchoMode(MeadowSerialDevice meadow)
        {
            if (meadow == null)
            {
                Console.WriteLine("No current device");
                return;
            }

            if (meadow.SerialPort == null)
            {
                Console.WriteLine("No current serial port");
                return;
            }

            meadow.Initialize(true);
        }

    }
}