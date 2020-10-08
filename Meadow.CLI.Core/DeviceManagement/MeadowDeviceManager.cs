using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Management;
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
        internal const UInt16 DefaultVS2019DebugPort = 4024;  // Port used by VS 2019

        // Note: While not truly important, it can be noted that size of the s25fl QSPI flash
        // chip's "Page" (i.e. the smallest size it can program) is 256 bytes. By making the
        // maxmimum data block size an even multiple of 256 we insure that each packet received
        // can be immediately written to the s25fl QSPI flash chip.
        internal const int MaxAllowableDataBlock = 512;
        internal const int MaxSizeOfPacketBuffer = MaxAllowableDataBlock + (MaxAllowableDataBlock / 254) + 8;
        internal const int ProtocolHeaderSize = 12;
        internal const int MaxDataSizeInProtocolMsg = MaxAllowableDataBlock - ProtocolHeaderSize;

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
            catch (Exception ex)
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

        public static List<string> GetSerialDeviceCaptions()
        {
            var devices = new List<string>();

            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption like '%(COM%'"))
            {
                var portnames = SerialPort.GetPortNames();
                foreach (var item in searcher.Get())
                {
                    devices.Add(item["Caption"].ToString());
                }
            }
            return devices;
        }

        //providing a numeric (0 = none, 1 = info and 2 = debug)
        public static void SetTraceLevel(MeadowSerialDevice meadow, int level)
        {
            if (level < 0 || level > 3)
                throw new System.ArgumentOutOfRangeException(nameof(level), "Trace level must be between 0 & 3 inclusive");

            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_CHANGE_TRACE_LEVEL;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint)level);
        }

        public static async Task<bool> ResetMeadow(MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESET_PRIMARY_MCU;
            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint)userData);
            await Task.Delay(3000);
            return true;
        }

        public static void EnterDfuMode(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENTER_DFU_MODE;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static void NshEnable(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint) 1);
        }

        public static async Task<bool> MonoDisable(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_DISABLE;
            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
            await Task.Delay(3000);
            return true;
        }

        public static async Task<bool> MonoEnable(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_ENABLE;
            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
            await Task.Delay(3000);
            return true;
        }

        public static void MonoRunState(MeadowSerialDevice meadow)
        {
             _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_RUN_STATE;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static async Task<bool> MonoFlash(MeadowSerialDevice meadow)
        {
             _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_FLASH;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);

            return await WaitForResponseMessage(meadow, x => x.Message.StartsWith("Mono runtime successfully flashed"));
        }

        public static void GetDeviceInfo(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_DEVICE_INFORMATION;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static void SetDeveloper1(MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_1;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }
        public static void SetDeveloper2(MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_2;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }
        public static void SetDeveloper3(MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_3;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }

        public static void SetDeveloper4(MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_4;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }

        public static void TraceDisable(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_NO_TRACE_TO_HOST;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static void TraceEnable(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_SEND_TRACE_TO_HOST;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static void Uart1Apps(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_NO_TRACE_TO_UART;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static void Uart1Trace(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_SEND_TRACE_TO_UART;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static void RenewFileSys(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_PART_RENEW_FILE_SYS;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static void QspiWrite(MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_WRITE;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }

        public static void QspiRead(MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_READ;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }

        public static void QspiInit(MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_INIT;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }

        // This method is called to sent to Visual Studio debugging to Mono
        public static void ForwardVisualStudioDataToMono(byte[] debuggingData, MeadowSerialDevice meadow, int userData)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEBUGGER_MSG;

            new SendTargetData(meadow).BuildAndSendSimpleData(debuggingData, _meadowRequestType, (uint)userData);
        }

        // This method is called to forward from mono debugging to Visual Studio
        public static void ForwardMonoDataToVisualStudio(byte[] debuggerData)
        {
            debuggingServer.SendToVisualStudio(debuggerData);
        }

        // Enter VSDebug mode.
        public static void VSDebug(int vsDebugPort)
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

            if (meadow.SerialPort == null && meadow.Socket == null)
            {
                Console.WriteLine("No current serial port or socket");
                return;
            }

            meadow.Initialize(true);
        }

        public static void Esp32ReadMac(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_READ_ESP_MAC_ADDRESS;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static void Esp32Restart(MeadowSerialDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESTART_ESP32;

            new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static async Task<bool> WaitForResponseMessage(MeadowSerialDevice meadow, Predicate<MeadowMessageEventArgs> filter, int millisecondDelay = 300000)
        {
            var tcs = new TaskCompletionSource<bool>();
            var result = false;

            EventHandler<MeadowMessageEventArgs> handler = (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Message) && filter(e))
                {
                    tcs.SetResult(true);
                    result = true;
                }
            };

            if (meadow.DataProcessor != null) meadow.DataProcessor.OnReceiveData += handler;

            await Task.WhenAny(new Task[] { tcs.Task, Task.Delay(millisecondDelay) });

            if (meadow.DataProcessor != null) meadow.DataProcessor.OnReceiveData -= handler;

            return result;
        }
    }
}