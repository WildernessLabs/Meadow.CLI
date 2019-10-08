using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Threading.Tasks;
using MeadowCLI.Hcom;
using System.Linq;

namespace MeadowCLI.DeviceManagement
{
    //a simple model object that represents a meadow device including connection
    public class MeadowSerialDevice : MeadowDevice
    {
        public EventHandler<MeadowMessageEventArgs> OnMeadowMessage;

        public SerialPort SerialPort { get; private set; }
        private string _serialPortName;
        public string PortName => SerialPort == null ? _serialPortName : SerialPort.PortName;

        private MeadowSerialDataProcessor dataProcessor;

        public MeadowSerialDevice(string serialPortName)
        {
            this._serialPortName = serialPortName;   
        }

        public static string[] GetAvailableSerialPorts()
        {
            return SerialPort.GetPortNames();
        }

        public bool Initialize(bool listen = true)
        {
            if (SerialPort != null)
            {
                SerialPort.Close();
                SerialPort = null;
            }

            if (OpenSerialPort(_serialPortName) == false)
                return false;

            if (listen == true)
            {
                ListenForSerialData();
            }
            return true;
        }

        public async Task DeployRequiredLibs(string path, bool forceUpdate = false)
        {
            if (forceUpdate || await IsFileOnDevice(SYSTEM).ConfigureAwait(false) == false)
            {
                await WriteFile(SYSTEM, path).ConfigureAwait(false);
            }

            if (forceUpdate || await IsFileOnDevice(SYSTEM_CORE).ConfigureAwait(false) == false)
            {
                await WriteFile(SYSTEM_CORE, path).ConfigureAwait(false);
            }

            if (forceUpdate || await IsFileOnDevice(MSCORLIB).ConfigureAwait(false) == false)
            {
                await WriteFile(MSCORLIB, path).ConfigureAwait(false);
            }

            if (forceUpdate || await IsFileOnDevice(MEADOW_CORE).ConfigureAwait(false) == false)
            {
                await WriteFile(MEADOW_CORE, path).ConfigureAwait(false);
            }
        }

        public async Task<bool> DeployApp(string path)
        {
            await WriteFile(APP_EXE, path);

            //get list of files in folder
            var files = Directory.GetFiles(path, "*.dll");

            //currently deploys all included dlls, update to use CRCs to only deploy new files
            //will likely need to update to deploy other files types (txt, jpg, etc.)
            foreach (var f in files)
            {
                var file = Path.GetFileName(f);
                if (file == MSCORLIB || file == SYSTEM || file == SYSTEM_CORE || file == MEADOW_CORE)
                    continue;

                await WriteFile(file, path);
            }

            return true; //can probably remove bool return type
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

        public override async Task<List<string>> GetFilesOnDevice(bool refresh = false, int timeoutInMs = 10000)
        {
            if (SerialPort == null)
            {
                throw new Exception("SerialPort not intialized");
            }

            if (filesOnDevice.Count == 0 || refresh == true)
            {
                var timeOutTask = Task.Delay(timeoutInMs);

                EventHandler<MeadowMessageEventArgs> handler = null;

                var tcs = new TaskCompletionSource<bool>();

                handler = (s, e) =>
                {
                    if (e.MessageType == MeadowMessageType.FileListMember)
                    {
                        SetFilesOnDeviceFromMessage(e.Message);
                        tcs.SetResult(true);
                    }
                };
                dataProcessor.OnReceiveData += handler;

                MeadowFileManager.ListFiles(this);

                await Task.WhenAny(new Task[] { timeOutTask, tcs.Task });
                dataProcessor.OnReceiveData -= handler;
            }

            return filesOnDevice;
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
                    if(!String.IsNullOrEmpty(args.Message))
                        Console.WriteLine("Data: " + args.Message);
                    break;
                case MeadowMessageType.AppOutput:
                    Console.WriteLine("App: " + args.Message);
                    break;
                case MeadowMessageType.FileListTitle:
                    Console.WriteLine("File List: ");
                    break;
                case MeadowMessageType.FileListMember:
                    Console.WriteLine(args.Message);
                    break;
                case MeadowMessageType.DeviceInfo:
                    SetDeviceIdFromMessage(args.Message);
                    Console.WriteLine("ID: " + args.Message);
                    break;
            }
        }

        void SetFilesOnDeviceFromMessage(string message)
        {
            var fileList = message.Split(',');
            
            filesOnDevice.Clear();

            foreach (var path in fileList)
            {
                var file = path.Substring(path.LastIndexOf('/') + 1);
                filesOnDevice.Add(file.Trim());
            }
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
    }
}