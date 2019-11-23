using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading.Tasks;
using MeadowCLI.Hcom;

namespace MeadowCLI.DeviceManagement
{
    //a simple model object that represents a meadow device including connection
    public class MeadowSerialDevice : MeadowDevice
    {
        public EventHandler<MeadowMessageEventArgs> OnMeadowMessage;

        public bool Verbose { get; protected set; }

        public SerialPort SerialPort { get; private set; }
        private string serialPortName;
        public string PortName => SerialPort == null ? serialPortName : SerialPort.PortName;

        private MeadowSerialDataProcessor dataProcessor;

        public MeadowSerialDevice(string serialPortName, bool verbose = true)
        {
            this.serialPortName = serialPortName;
            Verbose = verbose;
        }

        public static string[] GetAvailableSerialPorts()
        {
            return SerialPort.GetPortNames();
        }

        public bool Initialize(bool listen = true)
        {
            if (SerialPort != null)
            {
                SerialPort.Close();  // note: exception in ReadAsync
                SerialPort = null;
            }

            if (OpenSerialPort(serialPortName) == false)
                return false;

            if (listen == true)
            {
                ListenForSerialData();
            }
            return true;
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
                if (e.Message.Contains("File Sent Successfully"))
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
        public override async Task<bool> SetDeviceInfo(int timeoutInMs = 500)
        {
            var timeOutTask = Task.Delay(timeoutInMs);
            bool isDeviceIdSet = false;

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

            return isDeviceIdSet;
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
            catch
            {
                return false; //serial port couldn't be opened .... that's ok
            }
            return true;
        }

        internal void ListenForSerialData()
        {
            if (SerialPort != null)
            {
                dataProcessor = new MeadowSerialDataProcessor(SerialPort);

                dataProcessor.OnReceiveData += DataReceived;
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
                case MeadowMessageType.AppOutput:
                    ConsoleOut("App: " + args.Message);
                    break;
                case MeadowMessageType.MeadowDiag:
                    ConsoleOut("Diag: " + args.Message);
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
                    if(AttemptToReconnectToMeadow())
                        ConsoleOut("Successfully reconnected");
                    else
                        ConsoleOut("Failed to reconnect");
                    break;
            }
        }

        bool AttemptToReconnectToMeadow()
        {
            int delayCount = 20;    // 10 seconds
            while (true)
            {
                System.Threading.Thread.Sleep(500);

                bool portOpened = Initialize(true);
                if (portOpened)
                    return true;

                if (delayCount-- == 0)
                    return false;
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
    }
}