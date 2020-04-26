using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.DeviceManagement;
using Meadow.CLI.DeviceMonitor;
using MeadowCLI.Hcom;

namespace MeadowCLI.DeviceManagement
{
    //a simple model object that represents a meadow device including connection
    public class MeadowSerialDevice : MeadowDevice, IDisposable
    {
        public EventHandler<MeadowMessageEventArgs> OnMeadowMessage;

        public bool Verbose { get; protected set; }

        private SerialPort SerialPort { get; set; }
        private Socket Socket { get; set; }

        private MeadowSerialDataProcessor dataProcessor;
        private bool addAppOnNextOutput;
        
        const int bootRecoveryTimeMs = 200;
        private ManualResetEvent InitalizedWait = new ManualResetEvent(false);
        
        public EventHandler<String> ConsoleOutputText;


        public MeadowSerialDevice(bool verbose = true)
        {
            Verbose = verbose;
        }

        public MeadowSerialDevice(Connection connection, bool verbose = true)
        {
            Verbose = verbose;
            this.connection = connection;            
        }

        public enum DeviceStatus
        {
            Disconnected = 0,            
            USBConnected,
            PortOpen    = 100,
            PortOpenGotInfo,
            Reboot            
        }

        public EventHandler<DeviceStatus> StatusChange;
        private DeviceStatus _status = DeviceStatus.Disconnected;
        public DeviceStatus Status
        {
            get { return _status; }
            private set
            {
                if (_status != value)
                {
                    _status = value;
                    StatusChange?.Invoke(this, value);
                    StatusChangeAction(value);
                }
            }
        }

        public EventHandler<bool?> RunStateChange;
        private bool? _runState = false;
        public bool? RunState
        {
            get { return _runState; }
            private set
            {
                if (!_runState.HasValue || _runState.Value != value)
                {
                    _runState = value;
                    RunStateChange?.Invoke(this, value);
                }
            }
        }


        void StatusChangeAction(DeviceStatus newStatus)
        {
            switch (newStatus)
            {
                case DeviceStatus.USBConnected:
                    ConsoleOutput("Meadow: Usb Connected\n");
                    InitalizedWait.Reset();
                    OpenConnection();
                    break;
                case DeviceStatus.PortOpen:
                    ConsoleOutput("Meadow: Connected\n");
                    ListenForSerialData();
                    break;
                case DeviceStatus.PortOpenGotInfo:
                    ConsoleOutput("Meadow: Initalized\n");
                    InitalizedWait.Set();
                    break;
                case DeviceStatus.Disconnected:
                    ConsoleOutput("Meadow: Disconnected\n");
                    CloseConnection();
                    InitalizedWait.Reset();
                    break;
                case DeviceStatus.Reboot:
                    ConsoleOutput("Meadow: Reboot\n");
                    InitalizedWait.Reset();
                    break;
            }
        }
        
        
        Connection _connection = null;
        public Connection connection
        {
            get
            {
                return _connection;
            }
            set
            {
                if (connection != value && !value.Removed)
                {
                    if (_connection != null) _connection.RemovedEvent -= Connection_RemovedEvent;
                    _connection = value;
                    connection.RemovedEvent += Connection_RemovedEvent;
                    if (_connection.USB != null) Status = DeviceStatus.USBConnected;
                }
                else
                {
                    throw new Exception("Invalid connection reference");
                }
            }
        }


        public void SetImpendingRebootFlag()
        {
            Status = DeviceStatus.Reboot;
        }

        async public Task<bool> AwaitReboot(int Timeout)
        {
            //No use in starting a Task if already set
            if (InitalizedWait.WaitOne(0)) return true;

            return await Task<bool>.Run(() =>
            {
                var sw = new Stopwatch();
                sw.Start();
                var result = InitalizedWait.WaitOne(Timeout);
                sw.Stop();
                ConsoleOutput($"AwaitReboot: {sw.ElapsedMilliseconds}ms\n");
                return result;
            });
        }
        
        async public Task<DeviceStatus?> AwaitStatus(int timeoutInMs, params DeviceStatus[] status)
        {
            var timeOutTask = Task.Delay(timeoutInMs);
            var tcs = new TaskCompletionSource<bool>();
            DeviceStatus? result = null;
            ConsoleOutput($"AwaitStatus: {string.Join(",",status)}\n");
            var sw = new Stopwatch();
            sw.Start();
            EventHandler<DeviceStatus> handler = (s, e) =>
            {
                if (status.Contains(e))
                {
                    result = e;
                    tcs.SetResult(true);
                }
            };
           
            StatusChange += handler;
            await Task.WhenAny(new Task[] { timeOutTask, tcs.Task });
            StatusChange -= handler;
            sw.Stop();
            ConsoleOutput($"AwaitStatus: {result} {sw.ElapsedMilliseconds}ms\n");
            return result;
        }
        

        void Connection_RemovedEvent(object sender, EventArgs e)
        {            
            Status = DeviceStatus.Disconnected;
        }

        [ObsoleteAttribute("This property is obsolete. Use connection class.", false)]
        public static string[] GetAvailableSerialPorts()
        {
            return MeadowDeviceManager.FindSerialDevices().ToArray();
        }
        

        public static bool TryCreateIPEndPoint(string address,
            out IPEndPoint endpoint)
        {
            address = address.Replace("localhost", "127.0.0.1");
            endpoint = null;

            string[] ep = address.Split(':');
            if (ep.Length != 2)
                return false;

            if (!IPAddress.TryParse(ep[0], out IPAddress ip))
                return false;

            int port;
            if (!int.TryParse(ep[1], NumberStyles.None, NumberFormatInfo.CurrentInfo, out port))
                return false;

            endpoint = new IPEndPoint(ip, port);
            return true;
        }
        
        public bool IsConnected
        {
            get
            {          
                return (SerialPort?.IsOpen ?? false) || (Socket?.Connected ?? false);
            }
        }


        private async Task<bool> OpenConnection()
        {
            if (connection?.IP?.Endpoint != null)
            {
                Socket = new Socket(connection.IP.Endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                try
                {                
                    await Socket.ConnectAsync(connection.IP.Endpoint);
                    if (Socket.Connected)
                    {
                        Status = DeviceStatus.PortOpen;
                        return true;
                    }
                }
                catch (SocketException)
                {
                    ConsoleOutput("Could not connect to socket, aborting...\n");
                    Environment.Exit(1);
                }
            }
            else if (!String.IsNullOrEmpty(connection?.USB?.DevicePort) && !(SerialPort?.IsOpen ?? false))
            {
                if (OpenSerialPort(connection.USB.DevicePort))
                {
                    Status = DeviceStatus.PortOpen;
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> DeleteFile(string filename, int timeoutInMs = 5000)
        {
            if (SerialPort == null)
            {
                throw new Exception("SerialPort not intialized");
            }

            bool result = false;

            var timeOutTask = Task.Delay(timeoutInMs);

            EventHandler<MeadowMessageEventArgs> handler = null;

            var tcs = new TaskCompletionSource<bool>();

            handler = (s, e) =>
            {
                if (e.Message.Contains("deleted. No errors reported"))
                {
                    result = true;
                    tcs.SetResult(true);
                }
            };
            dataProcessor.OnReceiveData += handler;

            MeadowFileManager.DeleteFile(this, filename);

            await Task.WhenAny(new Task[] { timeOutTask, tcs.Task });
            dataProcessor.OnReceiveData -= handler;

            return result;
        }

        public override async Task<bool> WriteFile(string filename, string path, int timeoutInMs = 20000) //200s 
        {
            if (SerialPort == null)
            {
                throw new Exception("SerialPort not intialized");
            }

            bool result = false;

            var timeOutTask = Task.Delay(timeoutInMs);

            EventHandler<MeadowMessageEventArgs> handler = null;

            var tcs = new TaskCompletionSource<bool>();

            handler = (s, e) =>
            {
                if (e.Message.Contains("Download success"))
                {
                    result = true;
                    tcs.SetResult(true);
                }
            };
            dataProcessor.OnReceiveData += handler;
            
            MeadowFileManager.WriteFileToFlash(this, Path.Combine(path, filename), filename);

            await Task.WhenAny(new Task[] { timeOutTask, tcs.Task });
            dataProcessor.OnReceiveData -= handler;

            return result;
        }

        public override async Task<(List<string> files, List<UInt32> crcs)> GetFilesAndCrcs(int timeoutInMs = 60000)        
        {
            if (SerialPort == null)
            {
                throw new Exception("SerialPort not intialized");
            }

            var timeOutTask = Task.Delay(timeoutInMs);

            EventHandler<MeadowMessageEventArgs> handler = null;

            var tcs = new TaskCompletionSource<bool>();
            var started = false;

            handler = (s, e) =>
            {
                ConsoleOutput($"Msg: {e.MessageType}\n");

                if(e.MessageType == MeadowMessageType.FileListTitle)
                {
                    FilesOnDevice.Clear();
                    FileCrcs.Clear();
                    started = true;
                }
                else if(started == false)
                {   //ignore everything until we've started a new file list request
                    return;
                }

                if (e.MessageType == MeadowMessageType.FileListCrcMember)
                {
                    SetFileAndCrcsFromMessage(e.Message);
                } 

                if (e.MessageType == MeadowMessageType.Concluded)
                {
                    tcs.TrySetResult(true);
                } 
            };
            dataProcessor.OnReceiveData += handler;

            MeadowFileManager.ListFilesAndCrcs(this);

            await Task.WhenAny(new Task[] { timeOutTask, tcs.Task });
            dataProcessor.OnReceiveData -= handler; 

            return (FilesOnDevice, FileCrcs);
        }

        public override async Task<List<string>> GetFilesOnDevice(bool refresh = true, int timeoutInMs = 30000)
        {
            if (SerialPort == null)
            {
                throw new Exception("SerialPort not intialized");
            }

            if (FilesOnDevice.Count == 0 || refresh == true)
            {
                var timeOutTask = Task.Delay(timeoutInMs);

                EventHandler<MeadowMessageEventArgs> handler = null;

                var tcs = new TaskCompletionSource<bool>();
                bool started = false;

                handler = (s, e) =>
                {
                    if (e.MessageType == MeadowMessageType.FileListTitle)
                    {
                        FilesOnDevice.Clear();
                        FileCrcs.Clear();

                        started = true;
                    }
                    else if (started == false)
                    {   //ignore everything until we've started a new file list request
                        return;
                    }

                    if (e.MessageType == MeadowMessageType.FileListMember)
                    {
                        SetFileNameFromMessage(e.Message);
                    }

                    if(e.MessageType == MeadowMessageType.Concluded)
                    {
                        tcs.SetResult(true);
                    }
                };
                dataProcessor.OnReceiveData += handler;

                MeadowFileManager.ListFiles(this);

                await Task.WhenAny(new Task[] { timeOutTask, tcs.Task });
                dataProcessor.OnReceiveData -= handler;
            }

            return FilesOnDevice;
        }

        //device Id information is processed when the message is received
        //this will request the device Id and return true it was set successfully
        public override async Task<bool> SetDeviceInfo(int timeoutInMs = 3000)
        {            
            bool isDeviceIdSet = false;
            
            ConsoleOutput("Device info: Requesting\n");

            Stopwatch sw = new Stopwatch();
            sw.Start();
            EventHandler<MeadowMessageEventArgs> handler = null;

            var tcs = new TaskCompletionSource<bool>();

            handler = (s, e) =>
            {
                if (e.MessageType == MeadowMessageType.DeviceInfo)
                {
                    isDeviceIdSet = true;
                    tcs.SetResult(true);
                }
            };
            dataProcessor.OnReceiveData += handler;

            MeadowDeviceManager.GetDeviceInfo(this);
            
            await Task.WhenAny(new Task[] { Task.Delay(timeoutInMs), tcs.Task });
            dataProcessor.OnReceiveData -= handler;
            sw.Stop();

            ConsoleOut($"Device info: {(isDeviceIdSet ? "OK":"Timeout")} {sw.ElapsedMilliseconds}ms\n");

            if (isDeviceIdSet) Status = DeviceStatus.PortOpenGotInfo;
            return isDeviceIdSet;
        }
        
        //device Id information is processed when the message is received
        //this will request the device Id and return true it was set successfully
        public async Task<bool> GetRunState(int timeoutInMs = 3000)
        {
            var timeOutTask = Task.Delay(timeoutInMs);
            bool isRunStateSet = false;
            
            ConsoleOutput("Run State: Requesting.\n");
            
            Stopwatch sw = new Stopwatch();
            sw.Start();
            EventHandler<MeadowMessageEventArgs> handler = null;

            var tcs = new TaskCompletionSource<bool>();

            handler = (s, e) =>
            {
                if (e.MessageType == MeadowMessageType.Data
                && e.Message.StartsWith("On reset"))
                {
                    isRunStateSet = !e.Message.Contains("not");
                    tcs.SetResult(true);
                }
            };
            dataProcessor.OnReceiveData += handler;

            MeadowDeviceManager.MonoRunState(this);

            await Task.WhenAny(new Task[] { timeOutTask, tcs.Task });
            dataProcessor.OnReceiveData -= handler;
            sw.Stop();
            
            ConsoleOut($"Run State: {(isRunStateSet ? "Enabled":"Disabled")} {sw.ElapsedMilliseconds}ms\n");
            
            RunState = isRunStateSet;
            return isRunStateSet;
        }
        

        //putting this here for now ..... 
        bool OpenSerialPort(string portName)
        {
            
            ConsoleOutput($"OpenSerialPort: {portName} Opening.... ");

            if ((SerialPort?.IsOpen ?? false) == true)
            {
                ConsoleOutput("already open!");
                return true;
            }
            
            int msSinceConnection = (int)(DateTime.UtcNow - connection.TimeConnected).TotalMilliseconds;
            if (msSinceConnection < bootRecoveryTimeMs)
            {
                var waitabit = bootRecoveryTimeMs - msSinceConnection;
                ConsoleOutput($" waiting {waitabit}ms... ");
                Thread.Sleep(waitabit);
            }

        
            try
            {   // Create a new SerialPort object with default settings
                var port = new SerialPort
                {
                    PortName = portName,
                    BaudRate = 115200,       // This value is ignored when using ACM
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,

                    // Set the read/write timeouts
                    ReadTimeout = SerialPort.InfiniteTimeout,
                    WriteTimeout = 2000                    
                };

                port.Open();
                ConsoleOutput("Opened\n");

                //improves perf on Windows?
                port.BaseStream.ReadTimeout = 0;
                   
                if (port.BytesToRead > 0)
                {
                    ConsoleOutput($"Serial: Cleared {port.BytesToRead} bytes from rx buffer\n");
                    port.DiscardInBuffer();
                }
                
                if (port.BytesToWrite > 0)
                {
                    ConsoleOutput($"Serial: Cleared {port.BytesToRead} bytes from tx buffer\n");
                    port.DiscardOutBuffer();
                }

                SerialPort = port;
            }
            catch (Exception ex)
            {
                ConsoleOutput($"Error: {ex.Message}\n");
                return false;
            }
            return true;
        }

        internal void ListenForSerialData()
        {
            if (Socket != null)
            {
                dataProcessor = new MeadowSerialDataProcessor(Socket);
            } else if (SerialPort?.IsOpen ?? false)
            {
                dataProcessor = new MeadowSerialDataProcessor(SerialPort);
            }

            if (dataProcessor != null)
            {
                dataProcessor.OnReceiveData += DataReceived;
                dataProcessor.OnSocketClosed += DataProcessor_SocketClosed;
                if (Verbose) dataProcessor.ConsoleText += DataProcessor_ConsoleText;
                GetRunState(1000);
                SetDeviceInfo(1000);
                GetRunState(1000); //Possible meadow bug on first request after a reboot. Check again.
            }
        }

        void DataProcessor_ConsoleText(object sender, string text)
        {
            ConsoleOutputText?.Invoke(this,text);
        }



        void DataReceived(object sender, MeadowMessageEventArgs args)
        {
            OnMeadowMessage?.Invoke(this, args);

            if(args.MessageType != MeadowMessageType.AppOutput)
                addAppOnNextOutput = false;

            switch (args.MessageType)
            {
                case MeadowMessageType.Data:
                    ConsoleOut($"Data: {args.Message}\n" );
                    break;
                case MeadowMessageType.AppOutput:
                    // The received text is straight from mono via hcom and may not be
                    // correctly packetized.
                    // Previous this code assumed that each received block of text should
                    // begin with 'App :' and end with a new line. This was wrong.
                    // When the App sends a lot of text quickly the received boundaries
                    // can have no relation to the original App.exe author intended when
                    // using Console.Write or ConsoleOutput.
                    // This code creates new lines much more closly to the intent of the
                    // original App.exe author.
                    string[] oneLine = args.Message.Split('\n');
                    for(int i = 0; i < oneLine.Length; i++)
                    {
                        // The last array entry is a special case. If it's null or
                        // empty then the last character was  a single '\n' and
                        // we ignore it
                        if (i == oneLine.Length - 1)
                        {
                            if (!string.IsNullOrEmpty(oneLine[i]))
                            {
                                ConsoleOutNoEol("App: ");
                                ConsoleOutNoEol(oneLine[i]);
                            }
                        }
                        else
                        {
                            // Most typical line
                            ConsoleOutNoEol("App: ");
                            ConsoleOut(oneLine[i]);
                        }
                    }
                    break;
                case MeadowMessageType.MeadowTrace:
                    ConsoleOut("Trace: {args.Message}\n");
                    break;
                case MeadowMessageType.FileListTitle:
                    ConsoleOut("File List: \n");
                    break;
                case MeadowMessageType.FileListMember:
                    ConsoleOut(args.Message);
                    break;
                case MeadowMessageType.FileListCrcMember:
                    ConsoleOut(args.Message);
                    break;
                case MeadowMessageType.DeviceInfo:
                    //ToDo move this
                    SetDeviceIdFromMessage(args.Message);
                    ConsoleOut($"ID: {args.Message}\n");
                    break;
                case MeadowMessageType.SerialReconnect:
                    ConsoleOutput("MeadowMessageType.SerialReconnect\n");
                    //if(AttemptToReconnectToMeadow())
                    //    ConsoleOut("Successfully reconnected\n");
                    //else
                        //ConsoleOut("Failed to reconnect\n");
                    break;
            }
        }

        internal void SendData(byte[] encodedBytes, int start, int len)
        {
            if (Socket != null)
            {
                Socket.Send(encodedBytes, len,
                    System.Net.Sockets.SocketFlags.None);
            }
            else
            {
                if (SerialPort == null)
                    throw new ArgumentException("SerialPort cannot be null\n");

                SerialPort.Write(encodedBytes, start, len);
            }
        }

        
        void SetFileAndCrcsFromMessage(string fileListMember)
        {
            ConsoleOut($"SetFileAndCrcsFromMessage {fileListMember}\n");

            int fileNameStart = fileListMember.LastIndexOf('/') + 1;
            int crcStart = fileListMember.IndexOf('[') + 1;
            if(fileNameStart == 0 && crcStart == 0)
                return;     // No files found

            Debug.Assert(crcStart > fileNameStart);

            var file = fileListMember.Substring(fileNameStart, crcStart - fileNameStart - 2);
            FilesOnDevice.Add(file.Trim());

            var crc = Convert.ToUInt32(fileListMember.Substring(crcStart, 10), 16);
            FileCrcs.Add(crc);
        }

        void SetFileNameFromMessage(string fileListMember)
        {
            int fileNameStart = fileListMember.LastIndexOf('/') + 1;
            int crcStart = fileListMember.IndexOf('[') + 1;
            if(fileNameStart == 0 && crcStart == 0)
                return;     // No files found

            Debug.Assert(crcStart == 0);

            var file = fileListMember.Substring(fileNameStart, fileListMember.Length - fileNameStart);
            FilesOnDevice.Add(file.Trim());
        }

        /*"Meadow by Wilderness Labs,
        Model: F7Micro,
        MeadowOS Version: 0.1.0,
        Processor: STM32F777IIK6,
        Processor Id: 1d-00-29-00-12-51-36-30-33-33-37-33,
        Serial Number: 3360335A3036,
        CoProcessor: ESP32,
        CoProcessor OS Version: 0.1.x\r\n"
        */
        void SetDeviceIdFromMessage(string message)
        {
            var info = message.Split(new string[] { ",", ": " }, StringSplitOptions.None);

            if (info.Length < 15)
                return;

            DeviceInfo.Name = info[0];
            DeviceInfo.Model = info[2].TrimStart();
            DeviceInfo.MeadowOSVersion = info[4].TrimStart();
            DeviceInfo.Proccessor = info[6].TrimStart();
            DeviceInfo.ProcessorId = info[8].TrimStart();
            DeviceInfo.SerialNumber = info[10].TrimStart();
            DeviceInfo.CoProcessor = info[12].TrimStart();
            DeviceInfo.CoProcessorOs = info[14].TrimStart();
        }

        void ConsoleOut(string msg)
        {
            if(Verbose == false)
            {
                return;
            }

            ConsoleOutput(msg);
        }

        void ConsoleOutNoEol(string msg)
        {
            if(Verbose == false)
            {
                return;
            }

            ConsoleOutput(msg);
        }

        void DataProcessor_SocketClosed(object sender, EventArgs e)
        {
            if ((int)Status >= 100) Status = DeviceStatus.Disconnected;
        }

        void ConsoleOutput(string text)
        {
            ConsoleOutputText?.Invoke(this,text);
#if DEBUG
            Console.Write(text);
#endif
        }

        public void CloseConnection()
        {
            if (dataProcessor != null)
            {
                dataProcessor.OnReceiveData -= DataReceived;
                dataProcessor.OnSocketClosed -= DataProcessor_SocketClosed;
                if (Verbose) dataProcessor.ConsoleText -= DataProcessor_ConsoleText;
                dataProcessor = null;
            }
            SerialPort?.Dispose();
            Socket?.Dispose();            
        }
        
        public void Dispose()
        {
            CloseConnection();
        }
    }
}