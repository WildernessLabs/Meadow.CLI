using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Management;
using System.Management.Instrumentation;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI;
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
            var maxRetries = 15;
            var attempt = 0;

            MeadowSerialDevice meadow = null;

        connect:

            if (CurrentDevice?.SerialPort != null && CurrentDevice.SerialPort.IsOpen)
            {
                CurrentDevice.SerialPort.Dispose();
            }

            try
            {
                meadow = CurrentDevice = new MeadowSerialDevice(serialPort);
                meadow.Initialize();
                await meadow.GetDeviceInfo().ConfigureAwait(false);
                return meadow;
            }
            catch (Exception ex)
            {
                // sometimes the serial port needs time to reset
                if (attempt++ < maxRetries)
                {
                    await Task.Delay(500).ConfigureAwait(false);
                    goto connect;
                }
                else
                {
                    return null;
                }
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
        public static async Task SetTraceLevel(MeadowSerialDevice meadow, int level)
        {
            if (level < 0 || level > 3)
                throw new System.ArgumentOutOfRangeException(nameof(level), "Trace level must be between 0 & 3 inclusive");

            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_CHANGE_TRACE_LEVEL, userData: (uint)level);

            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_CHANGE_TRACE_LEVEL;
            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint)level);
        }

        public static async Task ResetMeadow(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESET_PRIMARY_MCU, doAcceptedCheck: false, filter: null);

            // needs some time to complete restart
            Thread.Sleep(1000);
        }

        public static async Task EnterDfuMode(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENTER_DFU_MODE);
            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENTER_DFU_MODE;
            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static async Task NshEnable(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH, userData: (uint)1);
            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH;
            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint) 1);
        }

        public static async Task MonoDisable(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_DISABLE, MeadowMessageType.SerialReconnect, timeoutMs: 15000);
        }

        public static async Task MonoEnable(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_ENABLE, MeadowMessageType.SerialReconnect, timeoutMs: 15000);
        }

        public static async Task MonoRunState(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_RUN_STATE);
            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_RUN_STATE;
            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static async Task MonoFlash(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_FLASH, timeoutMs: 200000, filter: e=> e.Message.StartsWith("Mono runtime successfully flashed."));
        }

        public static async Task<bool> GetDeviceInfo(MeadowSerialDevice meadow, int timeoutMs = 1000)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_DEVICE_INFORMATION;
            await new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
            return await WaitForResponseMessage(meadow, p => p.MessageType == MeadowMessageType.DeviceInfo, millisecondDelay: timeoutMs);
        }

        public static async Task SetDeveloper1(MeadowSerialDevice meadow, int userData)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_1, userData: (uint)userData);
            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_1;
            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }
        public static async Task SetDeveloper2(MeadowSerialDevice meadow, int userData)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_2, userData: (uint)userData);
            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_2;
            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }
        public static async Task SetDeveloper3(MeadowSerialDevice meadow, int userData)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_3, userData: (uint)userData);
            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_3;
            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }

        public static async Task SetDeveloper4(MeadowSerialDevice meadow, int userData)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_4, userData: (uint)userData);
            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_4;
            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }

        public static async Task TraceDisable(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_NO_TRACE_TO_HOST);
            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_NO_TRACE_TO_HOST;
            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static async Task TraceEnable(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_SEND_TRACE_TO_HOST);
            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_SEND_TRACE_TO_HOST;
            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static async Task Uart1Apps(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_NO_TRACE_TO_UART);

            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_NO_TRACE_TO_UART;

            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static async Task Uart1Trace(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_SEND_TRACE_TO_UART);
            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_SEND_TRACE_TO_UART;
            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static async Task RenewFileSys(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_PART_RENEW_FILE_SYS, MeadowMessageType.SerialReconnect);
            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_PART_RENEW_FILE_SYS;
            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static async Task QspiWrite(MeadowSerialDevice meadow, int userData)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_WRITE, userData: (uint)userData);
            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_WRITE;
            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }

        public static async Task QspiRead(MeadowSerialDevice meadow, int userData)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_READ, userData: (uint)userData);
            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_READ;
            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }

        public static async Task QspiInit(MeadowSerialDevice meadow, int userData)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_INIT, userData: (uint)userData);
            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_INIT;
            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType, (uint)userData);
        }

        // This method is called to sent to Visual Studio debugging to Mono
        public static void ForwardVisualStudioDataToMono(byte[] debuggerData, MeadowSerialDevice meadow, int userData)
        {
            // Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}-MDM-Forwarding {debuggerData.Length} bytes to Mono via hcom");
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEBUGGING_DEBUGGER_DATA;

            new SendTargetData(meadow).BuildAndSendSimpleData(debuggerData, _meadowRequestType, (uint)userData);
        }

        // This method is called to forward from mono debugging to Visual Studio
        public static void ForwardMonoDataToVisualStudio(byte[] debuggerData)
        {
            // Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}-MDM-Received {debuggerData.Length} bytes from hcom for VS");
            debuggingServer.SendToVisualStudio(debuggerData);
        }

        // Enter StartDebugging mode.
        public static async Task StartDebugging(MeadowSerialDevice meadow, int vsDebugPort)
        {
            // Tell meadow to start it's debugging server, after restarting.
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_START_DBG_SESSION;
            await new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);

            // The previous command caused Meadow to restart. Therefore, we must reestablish
            // Meadow communication.
            meadow.AttemptToReconnectToMeadow();

            // Create an instance of the TCP socket send/receiver class and
            // start it receiving.
            if (vsDebugPort == 0)
            {
                Console.WriteLine($"Without '--VSDebugPort' being specified, will assume Visual Studio 2019 using default port {DefaultVS2019DebugPort}");
                vsDebugPort = DefaultVS2019DebugPort;
            }

            // Start the local Meadow.CLI debugging server
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

        public static async Task Esp32ReadMac(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_READ_ESP_MAC_ADDRESS);
            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_READ_ESP_MAC_ADDRESS;
            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static async Task Esp32Restart(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESTART_ESP32);
            //_meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESTART_ESP32;
            //new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
        }

        public static async Task ProcessCommand(MeadowSerialDevice meadow, HcomMeadowRequestType requestType,
            MeadowMessageType responseMessageType = MeadowMessageType.Concluded, uint userData = 0, bool doAcceptedCheck = true, int timeoutMs = 10000)
        {
            await ProcessCommand(meadow, requestType, e => e.MessageType == responseMessageType, userData, doAcceptedCheck, timeoutMs);
        }

        public static async Task ProcessCommand(MeadowSerialDevice meadow, HcomMeadowRequestType requestType,
            Predicate<MeadowMessageEventArgs> filter, uint userData = 0, bool doAcceptedCheck = true, int timeoutMs = 10000)
        {
            await new SendTargetData(meadow).SendSimpleCommand(requestType, userData, doAcceptedCheck);
            var result = await WaitForResponseMessage(meadow, filter, timeoutMs);
            if (!result)
            {
                throw new MeadowDeviceManagerException(requestType);
            }
        }
        public static async Task<bool> WaitForResponseMessage(MeadowSerialDevice meadow, Predicate<MeadowMessageEventArgs> filter, int millisecondDelay = 10000)
        {
            if(filter == null)
            {
                return true;
            }

            var tcs = new TaskCompletionSource<bool>();
            var result = false;

            EventHandler<MeadowMessageEventArgs> handler = (s, e) =>
            {
                if (filter(e))
                {
                    result = true;
                    tcs.SetResult(true);
                }
            };

            if (meadow.DataProcessor != null) meadow.DataProcessor.OnReceiveData += handler;

            await Task.WhenAny(new Task[] { tcs.Task, Task.Delay(millisecondDelay) });

            if (meadow.DataProcessor != null) meadow.DataProcessor.OnReceiveData -= handler;

            return result;
        }
    }

    public class MeadowDeviceManagerException : Exception
    {
        public MeadowDeviceManagerException(HcomMeadowRequestType hcomMeadowRequestType)
        {
            HcomMeadowRequestType = hcomMeadowRequestType;
        }

        public HcomMeadowRequestType HcomMeadowRequestType { get; set; }
    }
}