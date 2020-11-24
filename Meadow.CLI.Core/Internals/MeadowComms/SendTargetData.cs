using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using static MeadowCLI.DeviceManagement.MeadowFileManager;
using MeadowCLI.DeviceManagement;
using System.Threading.Tasks;
using Meadow.CLI;

namespace MeadowCLI.Hcom
{
    public class SendTargetData
    {
        const int HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH = 12;
        const int HCOM_PROTOCOL_REQUEST_MD5_HASH_LENGTH = 32;
        const int HCOM_PROTOCOL_COMMAND_SEQ_NUMBER = 0;
        const UInt16 HCOM_PROTOCOL_CURRENT_VERSION_NUMBER = 0x0006;         // Used for transmission
        const UInt16 HCOM_PROTOCOL_EXTRA_DATA_DEFAULT_VALUE = 0x0000;       // Currently not used field

        //questioning if this class should send or just create the message
        MeadowSerialDevice _device; //refactor this .... 

        uint _packetCrc32;

        //==========================================================================
        // Constructor
        public SendTargetData(MeadowSerialDevice device, bool verbose = true)
        {
            _device = device;
            this.Verbose = verbose;
        }

        //==========================================================================

        public bool Verbose { get; protected set; }

        public void SendTheEntireFile(HcomMeadowRequestType requestType, string destFileName,
            uint partitionId, byte[] fileBytes, UInt32 mcuAddr, UInt32 payloadCrc32,
            string md5Hash, bool lastInSeries)
        {
            _packetCrc32 = 0;

            try
            {
                // Build and send the header
                BuildAndSendFileRelatedCommand(requestType,
                    partitionId, (UInt32)fileBytes.Length, payloadCrc32,
                    mcuAddr, md5Hash, destFileName);

                //--------------------------------------------------------------
                if (requestType == HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER)
                {
                  // For the ESP32 file download, the proceeding command will erase
                  // the ESP32 on chip flash memory before we can download. If the
                  // file is large enough, the time to erase the flash will prevent
                  // data from being downloaded and the 'semaphore timeout' error
                  // will cause the CLI to disconnect.
                  if((UInt32)fileBytes.Length > 1024 * 200)
                  {
                    // Using 6 ms / kbyte
                    int eraseDelay = (6 * fileBytes.Length) / 1000;
                    // Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}-Large file download delay:{eraseDelay} mSec");
                    System.Threading.Thread.Sleep(eraseDelay);
                  }
                }

                // Build each data packet
                int fileBufOffset = 0;
                int numbToSend;
                UInt16 sequenceNumber = 1;

                while (fileBufOffset <= fileBytes.Length - 1)           // equal would mean past the end
                {
                    if ((fileBufOffset + MeadowDeviceManager.MaxAllowableDataBlock) > (fileBytes.Length - 1))
                        numbToSend = fileBytes.Length - fileBufOffset;  // almost done, last packet
                    else
                        numbToSend = MeadowDeviceManager.MaxAllowableDataBlock;

                    BuildAndSendDataPacketRequest(fileBytes, fileBufOffset, numbToSend, sequenceNumber);
                    fileBufOffset += numbToSend;

                    sequenceNumber++;
                    //if (sequenceNumber % 1000 == 0)
                    //	Console.WriteLine("Have sent {0:N0} bytes out of {1:N0} in {2:N0} packets",
                    //		fileBufOffset, fileBytes.Length, sequenceNumber);
                }

                //--------------------------------------------------------------
                // Build and send the correct trailer
                switch(requestType)
                {
                    // Provide the correct message end depending on the reason the file
                    // is being downloaded to the F7 file system.
                    case HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER:
                        BuildAndSendSimpleCommand(HcomMeadowRequestType.HCOM_MDOW_REQUEST_END_FILE_TRANSFER,
                            lastInSeries ? (uint)1 : (uint)0);      // set UserData
                        break;
                    case HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_UPDATE_RUNTIME:
                        BuildAndSendSimpleCommand(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_UPDATE_FILE_END,
                            lastInSeries ? (uint)1 : (uint)0);      // set UserData
                        break;
                    case HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER:
                        BuildAndSendSimpleCommand(HcomMeadowRequestType.HCOM_MDOW_REQUEST_END_ESP_FILE_TRANSFER,
                            lastInSeries ? (uint)1 : (uint)0);      // set UserData
                        break;
                    default:
                        Console.WriteLine($"File end Meadow request type of {requestType} not defined");
                        break;
                }

                // bufferOffset should point to the byte after the last byte
                Debug.Assert(fileBufOffset == fileBytes.Length);
                if(Verbose) Console.WriteLine($"Total bytes sent {fileBufOffset:N0} in {sequenceNumber:N0} packets. PacketCRC:{_packetCrc32:x08}");
            }
            catch (Exception except)
            {
                Debug.WriteLine("{DateTime.Now:HH:mm:ss.fff}-Exception sending to Meadow:{0}", except);
                throw;
            }
        }

        //==========================================================================
        internal async Task SendSimpleCommand(HcomMeadowRequestType requestType, uint userData = 0, bool doAcceptedCheck = true)
        {
            var tcs = new TaskCompletionSource<bool>();
            var received = false;

            if (!doAcceptedCheck)
            {
                BuildAndSendSimpleCommand(requestType, userData);
                return;
            }

            EventHandler<MeadowMessageEventArgs> handler = (s, e) =>
            {
                if (e.MessageType == MeadowMessageType.Accepted)
                {
                    received = true;
                    tcs.SetResult(true);
                }
            };
            
            if (_device.DataProcessor != null) _device.DataProcessor.OnReceiveData += handler;

            BuildAndSendSimpleCommand(requestType, userData);

            await Task.WhenAny(new Task[] { tcs.Task, Task.Delay(10000) });

            if (_device.DataProcessor != null) _device.DataProcessor.OnReceiveData -= handler;

            if (!received)
            {
                throw new Exception("Command not accepted.");
            }
        }

        //==========================================================================
        // Prepare a data packet for sending
        private void BuildAndSendDataPacketRequest(byte[] messageBytes, int messageOffset,
            int messageSize, UInt16 seqNumb)
        {
            try
            {
                // Need to prepend the sequence number to the packet
                int xmitSize = messageSize + sizeof(UInt16);
                byte[] fullMsg = new byte[xmitSize];

                byte[] seqBytes = BitConverter.GetBytes(seqNumb);
                Array.Copy(seqBytes, fullMsg, sizeof(UInt16));
                Array.Copy(messageBytes, messageOffset, fullMsg, sizeof(UInt16), messageSize);

                EncodeAndSendPacket(fullMsg, 0, xmitSize);
            }
            catch (Exception except)
            {
                Console.WriteLine($"An exception was caught: {except}");
                throw;
            }
        }

        //==========================================================================
        // Build and send a "simple" message with data
        // Added for Visual Studio Debugging
        internal void BuildAndSendSimpleData(byte[] additionalData, HcomMeadowRequestType requestType, UInt32 userData)
        {
            int totalMsgLength = additionalData.Length + HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH;
            var messageBytes = new byte[totalMsgLength];

            // Populate the header
            BuildMeadowBoundSimpleCommand(requestType, userData, ref messageBytes);

            // Copy the payload into the message
            Array.Copy(additionalData, 0, messageBytes,
                HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH, additionalData.Length);

            EncodeAndSendPacket(messageBytes, 0, totalMsgLength);
        }

        //==========================================================================
        // Build and send a "simple" message with only a header
        internal void BuildAndSendSimpleCommand(HcomMeadowRequestType requestType, UInt32 userData)
        {
            var messageBytes = new byte[HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH];

            // Populate the header
            BuildMeadowBoundSimpleCommand(requestType, userData, ref messageBytes);
            EncodeAndSendPacket(messageBytes, 0, HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH);
        }

        //==========================================================================
        // This is most of the mandatory part of every non-data packet
        private int BuildMeadowBoundSimpleCommand(HcomMeadowRequestType requestType,
            UInt32 userData, ref byte[] messageBytes)
        {
            // Note: Could use the StructLayout attribute to build
            int offset = 0;

            // Two byte seq numb
            Array.Copy(BitConverter.GetBytes((UInt16)HCOM_PROTOCOL_COMMAND_SEQ_NUMBER), 0,
                messageBytes, offset, sizeof(UInt16));
            offset += sizeof(UInt16);

            // Protocol version
            Array.Copy(BitConverter.GetBytes((UInt16)HCOM_PROTOCOL_CURRENT_VERSION_NUMBER), 0, messageBytes, offset, sizeof(UInt16));
            offset += sizeof(UInt16);

            // Command type (2 bytes)
            Array.Copy(BitConverter.GetBytes((UInt16)requestType), 0, messageBytes, offset, sizeof(UInt16));
            offset += sizeof(UInt16);

            // Extra Data
            Array.Copy(BitConverter.GetBytes((UInt16)HCOM_PROTOCOL_EXTRA_DATA_DEFAULT_VALUE), 0, messageBytes, offset, sizeof(UInt16));
            offset += sizeof(UInt16);

            // User Data
            Array.Copy(BitConverter.GetBytes((UInt32)userData), 0, messageBytes, offset, sizeof(UInt32));
            offset += sizeof(UInt32);

            return offset;
        }

        //==========================================================================
        internal void BuildAndSendFileRelatedCommand(HcomMeadowRequestType requestType,
            UInt32 userData, UInt32 fileSize, UInt32 fileCheckSum, UInt32 mcuAddr,
            string md5Hash, string destFileName)
        {
            // Future: Try to use the StructLayout attribute
            Debug.Assert(md5Hash.Length == 0 || md5Hash.Length == HCOM_PROTOCOL_REQUEST_MD5_HASH_LENGTH);

            // Allocate the correctly size message buffers
            byte[] targetFileName = Encoding.UTF8.GetBytes(destFileName);           // Using UTF-8 works for ASCII but should be Unicode in nuttx
            byte[] md5HashBytes = Encoding.UTF8.GetBytes(md5Hash);
            int optionalDataLength = sizeof(UInt32) + sizeof(UInt32) + sizeof(UInt32) + 
                HCOM_PROTOCOL_REQUEST_MD5_HASH_LENGTH + targetFileName.Length;
            byte[] messageBytes = new byte[HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH + optionalDataLength];

            // Add the required header
            int offset = BuildMeadowBoundSimpleCommand(requestType, userData, ref messageBytes);

            // File Size
            Array.Copy(BitConverter.GetBytes(fileSize), 0, messageBytes, offset, sizeof(UInt32));
            offset += sizeof(UInt32);

            // CRC32 checksum or delete file partition number
            Array.Copy(BitConverter.GetBytes(fileCheckSum), 0, messageBytes, offset, sizeof(UInt32));
            offset += sizeof(UInt32);

            // MCU address for this file. Used for ESP32 file downloads
            Array.Copy(BitConverter.GetBytes(mcuAddr), 0, messageBytes, offset, sizeof(UInt32));
            offset += sizeof(UInt32);

            // Include ESP32 MD5 hash if it's needed
            if (string.IsNullOrEmpty(md5Hash))
                Array.Clear(messageBytes, offset, HCOM_PROTOCOL_REQUEST_MD5_HASH_LENGTH);
            else
                Array.Copy(md5HashBytes, 0, messageBytes, offset, HCOM_PROTOCOL_REQUEST_MD5_HASH_LENGTH);
            offset += HCOM_PROTOCOL_REQUEST_MD5_HASH_LENGTH;

            // Destination File Name
            Array.Copy(targetFileName, 0, messageBytes, offset, targetFileName.Length);
            offset += targetFileName.Length;

            Debug.Assert(offset == optionalDataLength + HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH);
            EncodeAndSendPacket(messageBytes, 0, offset);
        }

        //==========================================================================
        // Last stop before transmitting information
        private void EncodeAndSendPacket(byte[] messageBytes, int messageOffset, int messageSize)
        {
            try
            {
                // For testing calculate the crc including the sequence number
                _packetCrc32 = CrcTools.Crc32part(messageBytes, messageSize, 0, _packetCrc32);

                // Add 2, first to account for start delimiter and second for end
                byte[] encodedBytes = new byte[MeadowDeviceManager.MaxSizeOfPacketBuffer + 2];
                // Skip first byte so it can be a start delimiter
                int encodedToSend = CobsTools.CobsEncoding(messageBytes, messageOffset, messageSize, ref encodedBytes, 1);

                // Verify COBS - any delimiters left? Skip first byte
                for (int i = 1; i < encodedToSend; i++)
                {
                    if (encodedBytes[i] == 0x00)
                    {
                        throw new InvalidProgramException("All zeros should have been removed. " +
                            $"There's one at offset of {i}");
                    }
                }

                // Terminate packet with delimiter so packet boundaries can be more easily found
                encodedBytes[0] = 0;                // Start delimiter
                encodedToSend++;
                encodedBytes[encodedToSend] = 0;    // End delimiter
                encodedToSend++;

                try
                {
                    if (_device.Socket != null)
                    {
                        _device.Socket.Send(encodedBytes, encodedToSend,
                            System.Net.Sockets.SocketFlags.None);
                    }
                    else
                    {
                        if (_device.SerialPort == null)
                            throw new NotConnectedException();

                        _device.SerialPort.Write(encodedBytes, 0, encodedToSend);
                    }

                }
                catch (InvalidOperationException ioe)  // Port not opened
                {
                    Console.WriteLine("Write but port not opened. Exception: {0}", ioe);
                    throw;
                }
                catch (ArgumentOutOfRangeException aore)  // offset or count don't match buffer
                {
                    Console.WriteLine("Write buffer, offset and count don't line up. Exception: {0}", aore);
                    throw;
                }
                catch (ArgumentException ae)  // offset plus count > buffer length
                {
                    Console.WriteLine("Write offset plus count > buffer length. Exception: {0}", ae);
                    throw;
                }
                catch (TimeoutException te) // Took too long to send
                {
                    Console.WriteLine("Write took too long to send. Exception: {0}", te);
                    throw;
                }
            }
            catch (Exception except)
            {
                Debug.WriteLine($"EncodeAndSendPacket threw: {except}");
                throw;
            }
        }
    }

    public class NotConnectedException : Exception { }
}
