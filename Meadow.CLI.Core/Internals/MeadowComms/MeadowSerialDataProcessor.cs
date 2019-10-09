using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MeadowCLI.Hcom
{
    public enum MeadowMessageType
    {
        AppOutput,
        FileList,
        DeviceInfo,
        Data,
    }

    public class MeadowMessageEventArgs : EventArgs
    {
        public string Message { get; private set; }
        public MeadowMessageType MessageType { get; private set; }

        public MeadowMessageEventArgs (MeadowMessageType messageType, string message)
        {
            Message = message;
            MessageType = messageType;
        }
    }

    public class MeadowSerialDataProcessor
    {   //collapse to one and use enum
        public EventHandler<MeadowMessageEventArgs> OnReceiveData;

        readonly SerialPort serialPort;
        const int MAX_RECEIVED_BYTES = 2048;
        
        const string FILE_LIST_PREFIX = "FileList: "; 
        const string MONO_MSG_PREFIX =  "MonoMsg: ";
        const string DEVICE_INFO_PREFIX = "DevInfo: ";

        // It seems that the .Net SerialPort class is not all it could be.
        // To acheive reliable operation some SerialPort class methods must
        // not be used. When receiving, the BaseStream must be used.
        // http://www.sparxeng.com/blog/software/must-use-net-system-io-ports-serialport

        //-------------------------------------------------------------
        // Constructor
        public MeadowSerialDataProcessor(SerialPort serialPort)
        {
            this.serialPort = serialPort;

            ReadPortAsync(); 
        }

        //-------------------------------------------------------------
        // All received data handled here
        private async Task ReadPortAsync()
        {
            int offset = 0;
            byte[] buffer = new byte[MAX_RECEIVED_BYTES];

            try
            {
                while (true)
                {
                    var byteCount = Math.Min(serialPort.BytesToRead, buffer.Length);

                    if (byteCount > 0)
                    {
                        var receivedLength = await serialPort.BaseStream.ReadAsync(buffer, offset, byteCount).ConfigureAwait(false);
                        offset = AddAndProcessData(buffer, receivedLength + offset);
                        Debug.Assert(offset > -1);
                    }
                    await Task.Delay(50).ConfigureAwait(false);
                }
            }
            catch (ThreadAbortException ex)
            {
                //ignoring for now until we wire cancelation ...
                //this blocks the thread abort exception when the console app closes
            }
            catch (InvalidOperationException)
            {
                // common if the port is reset (e.g. mono enable/disable) - don't spew confusing info
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"Exception: {ex} may mean the target connection dropped");
            }
        }

        int AddAndProcessData(byte[] buffer, int availableBytes)
        {
            // Because of the way characters are received we must buffer until the terminating cr/lf
            // is detected. This implememtation is a quick and dirty way.
            var foundData  = new byte [MAX_RECEIVED_BYTES];
            int bytesUsed = 0;
            int recvOffset = 0;
            int foundOffset;

            do
            {
                Array.Clear(foundData, 0, MAX_RECEIVED_BYTES);      // FOR DEBUGGING

                for (foundOffset = 0; recvOffset < availableBytes;
                    recvOffset++, foundOffset++)
                {
                    if (buffer[recvOffset] == '\r' && buffer[recvOffset + 1] == '\n')
                    {
                        foundData[foundOffset] = buffer[recvOffset];
                        foundData[foundOffset + 1] = buffer[recvOffset + 1];
                        recvOffset += 2;
                        break;
                    }

                    foundData[foundOffset] = buffer[recvOffset];
                }

                if (foundData[foundOffset + 1] == '\n')
                {
                    var meadowMessage = Encoding.UTF8.GetString(foundData, 0, foundOffset + 2);
                    bytesUsed += foundOffset + 2;

                    if (meadowMessage.StartsWith(FILE_LIST_PREFIX))
                    {
                        // This is a comma separated list
                        string message = meadowMessage.Substring(FILE_LIST_PREFIX.Length);

                        OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.FileList, message));
                    }
                    else if (meadowMessage.StartsWith(MONO_MSG_PREFIX))
                    {
                        string message = meadowMessage.Substring(MONO_MSG_PREFIX.Length);

                        OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.AppOutput, message));
                    }
                    else if (meadowMessage.StartsWith(DEVICE_INFO_PREFIX))
                    {
                        string message = meadowMessage.Substring(DEVICE_INFO_PREFIX.Length);
                        OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.DeviceInfo, message));
                    }
                    else
                    {
                        OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.Data, meadowMessage));
                    }
                }

            } while (foundData[foundOffset + 1] == '\n');

            return availableBytes - bytesUsed;        // No full message remains
        }
    }
}