using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Internals.MeadowComms.RecvClasses;
using MeadowCLI.DeviceManagement;

namespace MeadowCLI.Hcom
{
    public enum MeadowMessageType
    {
        AppOutput,
        DeviceInfo,
        FileListTitle,
        FileListMember,
        Data,
    }

    public enum HcomProtocolCtrl
    {
        HcomProtoCtrlRequestUndefined,
        HcomProtoCtrlRequestRejected,
        HcomProtoCtrlRequestAccepted,
        HcomProtoCtrlRequestEnded,
        HcomProtoCtrlRequestError,
        HcomProtoCtrlRequestInformation,
        HcomProtoCtrlRequestFileListHeader,
        HcomProtoCtrlRequestFileListMember,
        HcomProtoCtrlRequestMonoMessage,
        HcomProtoCtrlRequestDeviceInfo,
        HcomProtoCtrlRequestDeviceDiag,
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
        HostCommBuffer _hostCommBuffer;
        RecvFactoryManager _recvFactoryManager;
        readonly SerialPort serialPort;

        const int MAX_RECEIVED_BYTES = 2048;

        // It seems that the .Net SerialPort class is not all it could be.
        // To acheive reliable operation some SerialPort class methods must
        // not be used. When receiving, the BaseStream must be used.
        // http://www.sparxeng.com/blog/software/must-use-net-system-io-ports-serialport

        //-------------------------------------------------------------
        // Constructor
        public MeadowSerialDataProcessor(SerialPort serialPort)
        {
            this.serialPort = serialPort;
            _recvFactoryManager = new RecvFactoryManager();
            _hostCommBuffer = new HostCommBuffer();
            _hostCommBuffer.Init(MeadowDeviceManager.maxAllowableDataBlock * 4);    // room for 4 messages

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
            HcomBufferReturn result;

            while (true)
            {
                // add these bytes to the circular buffer
                result = _hostCommBuffer.AddBytes(buffer, 0, availableBytes);
                if(result == HcomBufferReturn.HCOM_CIR_BUF_ADD_SUCCESS)
                {
                    break;
                }
                else if(result == HcomBufferReturn.HCOM_CIR_BUF_ADD_WONT_FIT)
                {
                    // Wasn't possible to put these bytes in the buffer. We need to
                    // process a few packets and then retry to add this data
                    result = PullAllPacketsFromBuffer();
                    if (result == HcomBufferReturn.HCOM_CIR_BUF_GET_FOUND_MSG)
                        continue;   // There should be room now for the failed add

                    if(result == HcomBufferReturn.HCOM_CIR_BUF_GET_NONE_FOUND ||
                        result == HcomBufferReturn.HCOM_CIR_BUF_GET_BUF_NO_ROOM)
                    {
                        // This should never happen
                        Debug.Assert(false);
                    }
                }
                else if(result == HcomBufferReturn.HCOM_CIR_BUF_ADD_BAD_ARG)
                {
                    // Something wrong with implemenation
                    Debug.Assert(false);
                }
                else
                {
                    // Undefined return value????
                    Debug.Assert(false);
                }
            }

            result = PullAllPacketsFromBuffer();
            Debug.Assert(result == HcomBufferReturn.HCOM_CIR_BUF_GET_FOUND_MSG ||
                result == HcomBufferReturn.HCOM_CIR_BUF_GET_NONE_FOUND);

            return 0;
        }

        HcomBufferReturn PullAllPacketsFromBuffer()
        {
            byte[] packetBuffer = new byte[MeadowDeviceManager.maxSizeOfXmitPacket];
            byte[] decodedBuffer = new byte[MeadowDeviceManager.maxAllowableDataBlock];
            int packetLength;
            HcomBufferReturn result;

            while (true)
            {
                result = _hostCommBuffer.GetNextPacket(packetBuffer, MeadowDeviceManager.maxAllowableDataBlock, out packetLength);
                if (result == HcomBufferReturn.HCOM_CIR_BUF_GET_NONE_FOUND)
                    return result;

                if(result == HcomBufferReturn.HCOM_CIR_BUF_GET_BUF_NO_ROOM)
                {
                    // Implementation problem the packetBuffer is too small
                    Debug.Assert(false);
                }

                // Only other possible outcome is success
                Debug.Assert(result == HcomBufferReturn.HCOM_CIR_BUF_GET_FOUND_MSG);

                // It's possible that we may find a series of 0x00 values int the buffer.
                // This is because when blocked the sender will attempt to send 0x00
                // before the full message. So when the connection is unblocked 0x00
                // is sent and put into the buffer.
                if (packetLength == 1)
                    continue;

                int decodedSize = CobsTools.CobsDecoding(packetBuffer, --packetLength, ref decodedBuffer);

                // Process the received packet
                if(decodedSize > 0)
                {
                    byte[] receivedMsg = new byte[decodedSize];
                    Array.Copy(decodedBuffer, 0, receivedMsg, 0, decodedSize);
                    bool procResult = ParseAndProcessDecodedPacket(receivedMsg);
                    if (procResult)
                        continue;   // See if there's another packet ready
                }

                // All errors just leave
                break;
            }
            return result;
        }

        bool ParseAndProcessDecodedPacket(byte[] receivedMsg)
        {
            try
            {
                IReceivedMessage processor = _recvFactoryManager.CreateProcessor(receivedMsg);
                if (processor.Execute(receivedMsg))
                {
                    ProcessRecvdMessage(processor.ToString(), processor.ProtocolCtrl);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex}");
                return false;
            }
        }

        void ProcessRecvdMessage(string meadowMessage, ushort protocolCtrl)
        {
            switch ((HcomProtocolCtrl)protocolCtrl)
            {
                case HcomProtocolCtrl.HcomProtoCtrlRequestUndefined:
                    OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.Data, "Request undefined"));
                    break;
                case HcomProtocolCtrl.HcomProtoCtrlRequestRejected:
                    OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.Data, meadowMessage));
                    break;
                case HcomProtocolCtrl.HcomProtoCtrlRequestAccepted:
                    break;
                case HcomProtocolCtrl.HcomProtoCtrlRequestEnded:
                    break;
                case HcomProtocolCtrl.HcomProtoCtrlRequestError:
                    OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.Data, meadowMessage));
                    break;
                case HcomProtocolCtrl.HcomProtoCtrlRequestInformation:
                    OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.Data, meadowMessage));
                    break;
                case HcomProtocolCtrl.HcomProtoCtrlRequestFileListHeader:
                    OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.FileListTitle, meadowMessage));
                  break;
                case HcomProtocolCtrl.HcomProtoCtrlRequestFileListMember:
                    OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.FileListMember, meadowMessage));
                    break;
                case HcomProtocolCtrl.HcomProtoCtrlRequestMonoMessage:
                    OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.AppOutput, meadowMessage));
                    break;
                case HcomProtocolCtrl.HcomProtoCtrlRequestDeviceInfo:
                    OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.DeviceInfo, meadowMessage));
                    break;
                case HcomProtocolCtrl.HcomProtoCtrlRequestDeviceDiag:
                    OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.Data, meadowMessage));
                    break;
                default:
                    Console.WriteLine("Received: default ");
                    throw new ArgumentException("Unknown protocol control type");
            }
        }

    }
}