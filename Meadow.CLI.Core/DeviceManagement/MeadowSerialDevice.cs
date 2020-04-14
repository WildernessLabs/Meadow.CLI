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


        void StatusChangeAction(DeviceStatus newStatus)
        {
            switch (newStatus)
            {
                case DeviceStatus.USBConnected:
                    Console.WriteLine("Meadow: Usb Connected");
                    InitalizedWait.Reset();
                    OpenConnection();
                    break;
                case DeviceStatus.PortOpen:
                    Console.WriteLine("Meadow: Connected");
                    ListenForSerialData();
                    break;
                case DeviceStatus.PortOpenGotInfo:
                    Console.WriteLine("Meadow: Initalized");
                    InitalizedWait.Set();
                    GetRunState(5000);
                    break;
                case DeviceStatus.Disconnected:
                    Console.WriteLine("Meadow: Disconnected");
                    CloseConnection();
                    InitalizedWait.Reset();
                    break;
                case DeviceStatus.Reboot:
                    Console.WriteLine("Meadow: Reboot");
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
                Console.WriteLine($"AwaitReboot: {sw.ElapsedMilliseconds}ms");
                return result;
            });
        }
        
        async public Task<DeviceStatus?> AwaitStatus(int timeoutInMs, params DeviceStatus[] status)
        {
            var timeOutTask = Task.Delay(timeoutInMs);
            var tcs = new TaskCompletionSource<bool>();
            DeviceStatus? result = null;
            Console.WriteLine($"AwaitStatus: {string.Join(",",status)}");
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
            Console.WriteLine($"AwaitStatus: {result} {sw.ElapsedMilliseconds}ms");
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


        private bool OpenConnection()
        {
            if (connection?.IP?.Endpoint != null)
            {
                Socket = new Socket(connection.IP.Endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    Socket.Connect(connection.IP.Endpoint);
                    Status = DeviceStatus.PortOpen;
                    return true;
                }
                catch (SocketException)
                {
                    Console.WriteLine("Could not connect to socket, aborting...");
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
                Console.WriteLine($"Msg: {e.MessageType}");

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
        public override async Task<bool> SetDeviceInfo(int timeoutInMs = 5000)
        {
            var timeOutTask = Task.Delay(timeoutInMs);
            bool isDeviceIdSet = false;
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

            await Task.WhenAny(new Task[] { timeOutTask, tcs.Task });
            dataProcessor.OnReceiveData -= handler;
            sw.Stop();

            ConsoleOut($"SetDeviceInfo: {isDeviceIdSet} {sw.ElapsedMilliseconds}ms");

            if (isDeviceIdSet) Status = DeviceStatus.PortOpenGotInfo;
            return isDeviceIdSet;
        }
        
        //device Id information is processed when the message is received
        //this will request the device Id and return true it was set successfully
        public async Task<bool> GetRunState(int timeoutInMs = 5000)
        {
            var timeOutTask = Task.Delay(timeoutInMs);
            bool isDeviceIdSet = false;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            EventHandler<MeadowMessageEventArgs> handler = null;

            var tcs = new TaskCompletionSource<bool>();

            handler = (s, e) =>
            {
                if (e.MessageType == MeadowMessageType.Data)
                {
                    isDeviceIdSet = true;
                    tcs.SetResult(true);
                }
            };
            dataProcessor.OnReceiveData += handler;

            MeadowDeviceManager.MonoRunState(this);

            await Task.WhenAny(new Task[] { timeOutTask, tcs.Task });
            dataProcessor.OnReceiveData -= handler;
            sw.Stop();

            ConsoleOut($"GetRunState: {isDeviceIdSet} {sw.ElapsedMilliseconds}ms");

            return isDeviceIdSet;
        }
        

        //putting this here for now ..... 
        bool OpenSerialPort(string portName)
        {
            
            Console.Write($"OpenSerialPort: {portName} Opening.... ");

            if ((SerialPort?.IsOpen ?? false) == true)
            {
                Console.WriteLine("already open!");
                return true;
            }
            
            int msSinceConnection = (int)(DateTime.UtcNow - connection.TimeConnected).TotalMilliseconds;
            if (msSinceConnection < bootRecoveryTimeMs)
            {
                var waitabit = bootRecoveryTimeMs - msSinceConnection;
                Console.Write($" waiting {waitabit}ms... ");
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
                    ReadTimeout = -1,
                    WriteTimeout = 5000
                };

                port.Open();
                Console.WriteLine("Opened");

                //improves perf on Windows?
                //   port.BaseStream.ReadTimeout = 0;
                if (port.BytesToRead > 0)
                {
                    Console.WriteLine($"Serial: Cleared {port.BytesToRead} bytes from rx buffer");
                    port.DiscardInBuffer();
                }

                SerialPort = port;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
            return true;
        }

        internal void ListenForSerialData()
        {
            if (Socket != null)
            {
                dataProcessor = new MeadowSerialDataProcessor(Socket);
                dataProcessor.OnReceiveData += DataReceived;
                dataProcessor.OnSocketClosed += DataProcessor_SocketClosed;
            } else if (SerialPort?.IsOpen ?? false)
            {
                dataProcessor = new MeadowSerialDataProcessor(SerialPort);
                dataProcessor.OnReceiveData += DataReceived;
                dataProcessor.OnSocketClosed += DataProcessor_SocketClosed;
            }
            Console.WriteLine("Requesting Device Info");
            SetDeviceInfo(5000);
        }



        void DataReceived(object sender, MeadowMessageEventArgs args)
        {
            OnMeadowMessage?.Invoke(this, args);

            if(args.MessageType != MeadowMessageType.AppOutput)
                addAppOnNextOutput = false;

            switch (args.MessageType)
            {
                case MeadowMessageType.Data:
                    ConsoleOut("Data: " + args.Message);
                    break;
                case MeadowMessageType.AppOutput:
                    // The received text is straight from mono via hcom and may not be
                    // correctly packetized.
                    // Previous this code assumed that each received block of text should
                    // begin with 'App :' and end with a new line. This was wrong.
                    // When the App sends a lot of text quickly the received boundaries
                    // can have no relation to the original App.exe author intended when
                    // using Console.Write or Console.WriteLine.
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
                    ConsoleOut("Trace: " + args.Message);
                    break;
                case MeadowMessageType.FileListTitle:
                    ConsoleOut("File List: ");
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
                    ConsoleOut("ID: " + args.Message);
                    break;
                case MeadowMessageType.SerialReconnect:
                    Console.WriteLine("MeadowMessageType.SerialReconnect");
                    //if(AttemptToReconnectToMeadow())
                    //    ConsoleOut("Successfully reconnected");
                    //else
                        //ConsoleOut("Failed to reconnect");
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
                    throw new ArgumentException("SerialPort cannot be null");

                SerialPort.Write(encodedBytes, start, len);
            }
        }

        
        void SetFileAndCrcsFromMessage(string fileListMember)
        {
            ConsoleOut($"SetFileAndCrcsFromMessage {fileListMember}");

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
            var info = message.Split(',');

            if (info.Length < 8)
                return;

            DeviceInfo.Name = info[0];
            DeviceInfo.Model = info[1];
            DeviceInfo.MeadowOSVersion = info[2];
            DeviceInfo.Proccessor = info[3];
            DeviceInfo.ProcessorId = info[4];
            DeviceInfo.SerialNumber = info[5];
            DeviceInfo.CoProcessor = info[6];
            DeviceInfo.CoProcessorOs = info[7];
        }

        void ConsoleOut(string msg)
        {
            if(Verbose == false)
            {
                return;
            }

            Console.WriteLine(msg);
        }

        void ConsoleOutNoEol(string msg)
        {
            if(Verbose == false)
            {
                return;
            }

            Console.Write(msg);
        }

        void DataProcessor_SocketClosed(object sender, EventArgs e)
        {
            if ((int)Status >= 100) Status = DeviceStatus.Disconnected;
        }

        public void CloseConnection()
        {
            if (dataProcessor != null)
            {
                dataProcessor.OnReceiveData -= DataReceived;
                dataProcessor.OnSocketClosed -= DataProcessor_SocketClosed;
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