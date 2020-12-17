using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI;
using MeadowCLI.Hcom;

namespace MeadowCLI.DeviceManagement
{
    //a simple model object that represents a meadow device including connection
    public class MeadowSerialDevice : MeadowDevice
    {
        public EventHandler<MeadowMessageEventArgs> OnMeadowMessage;

        public bool Verbose { get; protected set; }

        public SerialPort SerialPort { get; private set; }
        public Socket Socket { get; private set; }

        private string serialPortName;
        public string PortName => SerialPort == null ? serialPortName : SerialPort.PortName;

        public MeadowSerialDataProcessor DataProcessor { get; private set; }

        public MeadowSerialDevice(string serialPortName, bool verbose = true)
        {
            this.serialPortName = serialPortName;
            Verbose = verbose;
        }

        public static string[] GetAvailableSerialPorts()
        {
            return SerialPort.GetPortNames();
        }

        public static bool TryCreateIPEndPoint(string address,
            out IPEndPoint endpoint)
        {
            if (string.IsNullOrEmpty(address))
            {
                address = string.Empty;
            }
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

        public bool Initialize(bool listen = true)
        {
            if (TryCreateIPEndPoint(serialPortName, out IPEndPoint endpoint))
            {
                Socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    Socket.Connect(endpoint);
                }
                catch (SocketException)
                {
                    Console.WriteLine("Could not connect to socket, aborting...");
                    Environment.Exit(1);
                }
            }
            else
            {
                if (SerialPort != null)
                {
                    SerialPort.Close();  // note: exception in ReadAsync
                    SerialPort = null;
                }

                if (OpenSerialPort(serialPortName) == false)
                    return false;
            }

            if (listen == true)
            {
                ListenForSerialData();
            }
            return true;
        }

        public async Task<bool> DeleteFile(string filename, int timeoutInMs = 10000)
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
                if (e.Message.StartsWith("Delete success"))
                {
                    result = true;
                    tcs.SetResult(true);
                }
            };
            DataProcessor.OnReceiveData += handler;

            await MeadowFileManager.DeleteFile(this, filename);

            await Task.WhenAny(new Task[] { timeOutTask, tcs.Task });
            DataProcessor.OnReceiveData -= handler;

            return result;
        }

        public override async Task<bool> WriteFile(string filename, string path, int timeoutInMs = 200000) //200s 
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
                if (e.MessageType == MeadowMessageType.Concluded)
                {
                    result = true;
                    tcs.SetResult(true);
                }
            };
            DataProcessor.OnReceiveData += handler;

            await MeadowFileManager.WriteFileToFlash(this, Path.Combine(path, filename), filename);

            await Task.WhenAny(new Task[] { timeOutTask, tcs.Task });
            DataProcessor.OnReceiveData -= handler;

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
            DataProcessor.OnReceiveData += handler;

            await MeadowFileManager.ListFilesAndCrcs(this).ConfigureAwait(false);

            await Task.WhenAny(new Task[] { timeOutTask, tcs.Task });
            DataProcessor.OnReceiveData -= handler; 

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
                DataProcessor.OnReceiveData += handler;

                await MeadowFileManager.ListFiles(this);

                await Task.WhenAny(new Task[] { timeOutTask, tcs.Task });
                DataProcessor.OnReceiveData -= handler;
            }

            return FilesOnDevice;
        }

        //device Id information is processed when the message is received
        //this will request the device Id and return true it was set successfully
        public override async Task GetDeviceInfo(int timeoutInMs = 1000)
        {
            var result = await MeadowDeviceManager.GetDeviceInfo(this, timeoutInMs);
            if (!result)
            {
                throw new DeviceInfoException();
            }
        }

        //device name is processed when the message is received
        //this will request the device name and return true it was successfully
        public override async Task GetDeviceName(int timeoutInMs = 1000)
        {
            var result = await MeadowDeviceManager.GetDeviceName(this, timeoutInMs);
            if (!result)
            {
                throw new DeviceInfoException();
            }
        }

        //putting this here for now ..... 
        bool OpenSerialPort(string portName)
        {
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
                    ReadTimeout = 5000,
                    WriteTimeout = 5000
                };

                port.Open();

                //improves perf on Windows?
                port.BaseStream.ReadTimeout = 0;

                SerialPort = port;
            }
            catch(Exception)
            {
                return false; //serial port couldn't be opened .... that's ok
            }
            return true;
        }

        internal void ListenForSerialData()
        {
            if (Socket != null)
            {
                DataProcessor = new MeadowSerialDataProcessor(Socket);

                DataProcessor.OnReceiveData += DataReceived;
            } else if (SerialPort != null)
            {
                DataProcessor = new MeadowSerialDataProcessor(SerialPort);

                DataProcessor.OnReceiveData += DataReceived;
            }
        }

        void DataReceived(object sender, MeadowMessageEventArgs args)
        {
            OnMeadowMessage?.Invoke(this, args);

            switch (args.MessageType)
            {
                case MeadowMessageType.Data:
                    ConsoleOut("Data: " + args.Message);
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
                    AttemptToReconnectToMeadow();
                    break;
                    // The last 2 types received text straight from mono' stdout / stderr
                    // via hcom and may not be packetized at the end of a lines.
                case MeadowMessageType.ErrOutput:
                    ParseAndOutputStdioText(args.Message, "Err: ");
                    break;
                case MeadowMessageType.AppOutput:
                    ParseAndOutputStdioText(args.Message, "App: ");
                    break;
            }
        }

        // Previously this code assumed that each received block of text should
        // begin with 'App :' and end with a new line.
        // However, when the App sends a lot of text quickly the packet's boundaries
        // can have no relation to the original App.exe author's intended when
        // using Console.Write or Console.WriteLine.
        // This code creates new lines much more closly to the intent of the
        // original App.exe author. It looks for the new line as an indication
        // that a new line is needed, because some packets have not new line.
        private void ParseAndOutputStdioText(string message, string leadInText)
        {
            // Note: There may have already been a part of a line received and
            // displayed (i.e. our screen cursor may be at the end of a text
            // that doesn't represent a full line. So we may need to finish
            // the line and then add our new line.
            // 
            // Break the data into whatever lines we can
            // If a normal line is received it will be split into 2 array
            // entries. The first all the text and the second empty and
            // ignored
            string[] oneLine = message.Split('\n');
            for (int i = 0; i < oneLine.Length; i++)
            {
                // The last oneLine array entry is a special case. If it's null or
                // empty then the last character received was a single '\n' and
                // we can ignore it
                if (i == oneLine.Length - 1)
                {
                    // Last oneLine array entry. 
                    if (!string.IsNullOrEmpty(oneLine[i]))
                    {
                        // There's text on this line but no '\n'
                        ConsoleOutNoEol(leadInText);
                        ConsoleOutNoEol(oneLine[i]);
                    }
                }
                else
                {
                    // Most typical lines have new line at end
                    ConsoleOutNoEol(leadInText);
                    ConsoleOut(oneLine[i]);
                }
            }
        }

        internal bool AttemptToReconnectToMeadow()
        {
            //close port ASAP to solve plotblem with old win7 usbser.sys driver
            SerialPort?.Close();

            int delayCount = 20;    // 10 seconds
            while (true)
            {
                System.Threading.Thread.Sleep(500);

                bool portOpened = Initialize(true);
                if (portOpened)
                {
                    Thread.Sleep(2000);
                    return true;
                }

                if (delayCount-- == 0)
                    throw new NotConnectedException();
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
    }

    public class DeviceInfoException : Exception { }
}