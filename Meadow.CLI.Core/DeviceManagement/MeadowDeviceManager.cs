﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Threading;
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

        internal const int MaxAllowableMsgPacketLength = 512;
        internal const int MaxEstimatedSizeOfEncodedPayload = MaxAllowableMsgPacketLength + (MaxAllowableMsgPacketLength / 254) + 8;
        internal const int ProtocolHeaderSize = 12;
        internal const int MaxAllowableMsgPayloadLength = MaxAllowableMsgPacketLength - ProtocolHeaderSize;

        static HcomMeadowRequestType _meadowRequestType;
        static DebuggingServer debuggingServer;

        static readonly string _systemHttpNetDllName = "System.Net.Http.dll";

        static MeadowDeviceManager()
        {
            // TODO: populate the list of attached devices

            // TODO: wire up listeners for device plug and unplug
        }

        static Dictionary<string, MeadowSerialDevice> _connections = new Dictionary<string, MeadowSerialDevice>();

        public static async Task<MeadowSerialDevice> GetMeadowForSerialPort(string serialPort) //, bool verbose = true)
        {
            try
            {
                if (_connections.ContainsKey(serialPort))
                {
                    _connections[serialPort].Dispose();
                    _connections.Remove(serialPort);
                    Thread.Sleep(1000);
                }
                var meadow = new MeadowSerialDevice(serialPort);
                meadow.Initialize();
                _connections.Add(serialPort, meadow);
                return meadow;

            }
            catch (Exception ex)
            {
                throw ex;
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
        }

        public static async Task NshEnable(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH, userData: (uint)1);
        }

        public static async Task MonoDisable(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_DISABLE, MeadowMessageType.SerialReconnect, timeoutMs: 15000);
        }

        public static async Task MonoEnable(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_ENABLE, MeadowMessageType.SerialReconnect, timeoutMs: 15000);
        }

        public static async Task<bool> MonoRunState(MeadowSerialDevice meadow)
        {
            await new SendTargetData(meadow).SendSimpleCommand(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_RUN_STATE);

            var tcs = new TaskCompletionSource<bool>();
            var result = false;

            EventHandler<MeadowMessageEventArgs> handler = (s, e) =>
            {
                if (e.MessageType == MeadowMessageType.Data)
                {
                    if (e.Message == "On reset, Meadow will start MONO and run app.exe")
                    {
                        result = true;
                        tcs.SetResult(true);
                    }
                    else if (e.Message == "On reset, Meadow will not start MONO, therefore app.exe will not run")
                    {
                        result = false;
                        tcs.SetResult(true);
                    }
                }
            };

            if (meadow.DataProcessor != null) meadow.DataProcessor.OnReceiveData += handler;

            await Task.WhenAny(new Task[] { tcs.Task, Task.Delay(5000) });

            if (meadow.DataProcessor != null) meadow.DataProcessor.OnReceiveData -= handler;

            return result;
        }

        public static async Task MonoFlash(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_FLASH, timeoutMs: 200000, filter: e => e.Message.StartsWith("Mono runtime successfully flashed."));
        }

        public static async Task<(bool isSuccessful, string message, MeadowMessageType msgType)> GetDeviceInfo(MeadowSerialDevice meadow, int timeoutMs = 1000)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_DEVICE_INFORMATION;
            await new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
            return await WaitForResponseMessage(meadow, p => p.MessageType == MeadowMessageType.DeviceInfo, millisecondDelay: timeoutMs);
        }

        public static async Task<string> GetDeviceSerialNumber(MeadowSerialDevice meadow, int timeoutMs = 1000)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_DEVICE_INFORMATION;
            await new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
            var result =  await WaitForResponseMessage(meadow, p => p.MessageType == MeadowMessageType.DeviceInfo, millisecondDelay: timeoutMs);
            if (result.isSuccessful)
            {
                return ParseDeviceInfo(result.message, "Serial Number: ", ",");
            }

            return string.Empty;
        }

        private static string ParseDeviceInfo(string deviceInfo, string value, string endChar)
        {
            var start = deviceInfo.IndexOf(value) + value.Length;
            var end = deviceInfo.IndexOf(endChar, start);
            return deviceInfo.Substring(start, end-start);
        }

        public static async Task<(bool isSuccessful, string message, MeadowMessageType msgType)> GetDeviceName(MeadowSerialDevice meadow, int timeoutMs = 1000)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_DEVICE_NAME;
            await new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
            return await WaitForResponseMessage(meadow, p => p.MessageType == MeadowMessageType.DeviceInfo, millisecondDelay: timeoutMs);
        }

        public static async Task SetDeveloper1(MeadowSerialDevice meadow, int userData)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_1, userData: (uint)userData);
        }
        public static async Task SetDeveloper2(MeadowSerialDevice meadow, int userData)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_2, userData: (uint)userData);
        }
        public static async Task SetDeveloper3(MeadowSerialDevice meadow, int userData)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_3, userData: (uint)userData);
        }

        public static async Task SetDeveloper4(MeadowSerialDevice meadow, int userData)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_4, userData: (uint)userData);
        }

        public static async Task TraceDisable(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_NO_TRACE_TO_HOST);
        }

        public static async Task TraceEnable(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_SEND_TRACE_TO_HOST);
        }

        public static async Task Uart1Apps(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_NO_TRACE_TO_UART);
        }

        public static async Task Uart1Trace(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_SEND_TRACE_TO_UART);
        }

        public static async Task RenewFileSys(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_PART_RENEW_FILE_SYS, MeadowMessageType.SerialReconnect);
        }

        public static async Task QspiWrite(MeadowSerialDevice meadow, int userData)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_WRITE, userData: (uint)userData);
        }

        public static async Task QspiRead(MeadowSerialDevice meadow, int userData)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_READ, userData: (uint)userData);
        }

        public static async Task QspiInit(MeadowSerialDevice meadow, int userData)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_INIT, userData: (uint)userData);
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
            debuggingServer.StartListening(meadow);
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
        }

        public static async Task Esp32Restart(MeadowSerialDevice meadow)
        {
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESTART_ESP32);
        }

        //public static async Task
        //    GetInitialFileBytes(MeadowSerialDevice meadow, string fileName)
       // {
        //    await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_INITIAL_FILE_BYTES);

         //   _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_INITIAL_FILE_BYTES;
         //   await new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
         //   return await WaitForResponseMessage(meadow, p => p.MessageType == MeadowMessageType.Data, millisecondDelay: 1000);

            /*
            await ProcessCommand(meadow, HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_INITIAL_FILE_BYTES);

            await new SendTargetData(meadow).SendSimpleCommand(_meadowRequestType);
            return await WaitForResponseMessage(meadow, p => p.MessageType == MeadowMessageType.DeviceInfo, millisecondDelay: timeoutMs);
            */
      //  }

        public static async Task DeployApp(MeadowSerialDevice meadow, string applicationFilePath)
        {
            if (!File.Exists(applicationFilePath))
            {
                Console.WriteLine($"{applicationFilePath} not found.");
                return;
            }

            FileInfo fi = new FileInfo(applicationFilePath);
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // for some strange reason, System.Net.Http.dll doesn't get copied to the output folder in VS.
                // so, we need to copy it over from the meadow assemblies nuget.
                CopySystemNetHttpDll(fi.DirectoryName);
            }

            var deviceFile = await meadow.GetFilesAndCrcs();
            var extensions = new List<string> { ".exe", ".bmp", ".jpg", ".jpeg", ".json", ".xml", ".yml", ".txt" };

            var paths = Directory.EnumerateFiles(fi.DirectoryName, "*.*", SearchOption.TopDirectoryOnly)
            .Where(s => extensions.Contains(new FileInfo(s).Extension));

            var files = new List<string>();
            var crcs = new List<UInt32>();

            foreach (var file in paths)
            {
                using (FileStream fs = File.Open(file, FileMode.Open))
                {
                    var len = (int)fs.Length;
                    var bytes = new byte[len];

                    fs.Read(bytes, 0, len);

                    //0x
                    var crc = CrcTools.Crc32part(bytes, len, 0);// 0x04C11DB7);

                    //Console.WriteLine($"{file} crc is {crc}");
                    files.Add(Path.GetFileName(file));
                    crcs.Add(crc);
                }
            }

            var dependences = AssemblyManager.GetDependencies(fi.Name, fi.DirectoryName);

            //crawl dependences
            foreach (var file in dependences)
            {
                using (FileStream fs = File.Open(Path.Combine(fi.DirectoryName, file), FileMode.Open))
                {
                    var len = (int)fs.Length;
                    var bytes = new byte[len];

                    fs.Read(bytes, 0, len);

                    //0x
                    var crc = CrcTools.Crc32part(bytes, len, 0);// 0x04C11DB7);

                    Console.WriteLine($"{file} crc is {crc}");
                    files.Add(Path.GetFileName(file));
                    crcs.Add(crc);
                }
            }

            // delete unused files
            foreach (var file in deviceFile.files)
            {
                if (files.Contains(file) == false)
                {
                    await meadow.DeleteFile(file).ConfigureAwait(false);
                    Console.WriteLine($"Removing file: {file}");
                }
            }

            // write new files
            for (int i = 0; i < files.Count; i++)
            {
                if (deviceFile.crcs.Contains(crcs[i]))
                {
                    Console.WriteLine($"Skipping file: {files[i]}");
                    continue;
                }

                if (!File.Exists(Path.Combine(fi.DirectoryName, files[i])))
                {
                    Console.WriteLine($"{files[i]} not found");
                    continue;
                }

                await meadow.WriteFile(files[i], fi.DirectoryName);
                Console.WriteLine($"Writing file: {files[i]}");
            }

            Console.WriteLine($"{fi.Name} deploy complete");
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
            if (!result.isSuccessful)
            {
                throw new MeadowDeviceManagerException(requestType);
            }
        }
        public static async Task<(bool isSuccessful, string message, MeadowMessageType messageType)> WaitForResponseMessage(MeadowSerialDevice meadow, Predicate<MeadowMessageEventArgs> filter, int millisecondDelay = 10000)
        {
            if (filter == null)
            {
                return (true, string.Empty, MeadowMessageType.ErrOutput);
            }

            var tcs = new TaskCompletionSource<bool>();
            var result = false;
            var message = string.Empty;
            var messageType = MeadowMessageType.ErrOutput;
            
            EventHandler<MeadowMessageEventArgs> handler = (s, e) =>
            {
                if (filter(e))
                {
                    message = e?.Message;
                    messageType = e.MessageType;
                    result = true;
                    tcs.SetResult(true);
                }
            };

            if (meadow.DataProcessor != null) meadow.DataProcessor.OnReceiveData += handler;

            await Task.WhenAny(new Task[] { tcs.Task, Task.Delay(millisecondDelay) });

            if (meadow.DataProcessor != null) meadow.DataProcessor.OnReceiveData -= handler;

            return (result, message, messageType);
        }

        private static void CopySystemNetHttpDll(string targetDir)
        {
            try
            {
                var bclNugetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "wildernesslabs.meadow.assemblies");

                if (Directory.Exists(bclNugetPath))
                {
                    List<Version> versions = new List<Version>();

                    var versionFolders = Directory.EnumerateDirectories(bclNugetPath);
                    foreach (var versionFolder in versionFolders)
                    {
                        var di = new DirectoryInfo(versionFolder);
                        Version outVersion;
                        if (Version.TryParse(di.Name, out outVersion))
                        {
                            versions.Add(outVersion);
                        }
                    }

                    if (versions.Any())
                    {
                        versions.Sort();

                        var sourcePath = Path.Combine(bclNugetPath, versions.Last().ToString(), "lib", "net472");
                        if (Directory.Exists(sourcePath))
                        {
                            if (File.Exists(Path.Combine(sourcePath, _systemHttpNetDllName)))
                            {
                                File.Copy(Path.Combine(sourcePath, _systemHttpNetDllName), Path.Combine(targetDir, _systemHttpNetDllName));
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // eat this for now
            }
        }
    }

    public class MeadowDeviceManagerException : Exception
    {
        public MeadowDeviceManagerException(HcomMeadowRequestType hcomMeadowRequestType)
        {
            HcomMeadowRequestType = hcomMeadowRequestType;
        }

        public HcomMeadowRequestType HcomMeadowRequestType { get; set; }

        public override string ToString()
        {
            return $"A {GetType()} exception has occurred while making a {nameof(HcomMeadowRequestType)} of {HcomMeadowRequestType}.";
        }
    }
}