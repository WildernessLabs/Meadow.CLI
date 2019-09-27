using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using static MeadowCLI.DeviceManagement.MeadowFileManager;

namespace MeadowCLI.Hcom
{
    public class SendTargetData
    {
        //                                                             seq+ver+ctl+cmd+user
        public const int HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH = 2 + 2 + 2 + 2 + 4;
        public const int HCOM_PROTOCOL_COMMAND_SEQ_NUMBER = 0;
        public const UInt16 HCOM_PROTOCOL_CURRENT_VERSION_NUMBER = 0x0002;
        public const UInt16 HCOM_PROTOCOL_CONTROL_VALUE_FUTURE = 0x0000;

        //questioning if this class should send or just create the message
        SerialPort _serialPort; //refactor this .... 

        uint _packetCrc32;

        // Note: While not truly important, it can be noted that, size of the s25fl QSPI flash
        // chip's "Page" (i.e. the smallest size it can program) is 256 bytes. By making the
        // maxmimum data block size an even multiple of 256 we insure that each packet received
        // can be immediately written to the s25fl QSPI flash chip.
        const int maxAllowableDataBlock = 512;
        const int maxSizeOfXmitPacket = (maxAllowableDataBlock + 4) + (maxAllowableDataBlock / 254);

        //==========================================================================
        // Constructor
        public SendTargetData(SerialPort serialPort)
        {
            if (serialPort == null)
                throw new ArgumentException("SerialPort cannot be null");

            _serialPort = serialPort;
        }

        //==========================================================================
        public void SendTheEntireFile(string destFileName, uint partitionId, byte[] fileBytes, UInt32 payloadCrc32)
        {
            _packetCrc32 = 0;

            try
            {
                // Build and send the header
                BuildAndSendFileRelatedCommand(
                    HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER,
                    partitionId, (UInt32)fileBytes.Length, payloadCrc32, destFileName);

                //--------------------------------------------------------------
                // Build all the data packets
                int fileBufOffset = 0;
                int numbToSend;
                UInt16 sequenceNumber = 1;

                while (fileBufOffset <= fileBytes.Length - 1)           // equal would mean past the end
                {
                    if ((fileBufOffset + maxAllowableDataBlock) > (fileBytes.Length - 1))
                        numbToSend = fileBytes.Length - fileBufOffset;  // almost done, last packet
                    else
                        numbToSend = maxAllowableDataBlock;

                    BuildAndSendDataPacketRequest(fileBytes, fileBufOffset, numbToSend, sequenceNumber);
                    fileBufOffset += numbToSend;

                    sequenceNumber++;
                    //if (sequenceNumber % 1000 == 0)
                    //	Console.WriteLine("Have sent {0:N0} bytes out of {1:N0} in {2:N0} packets",
                    //		fileBufOffset, fileBytes.Length, sequenceNumber);
                }

                //--------------------------------------------------------------
                // Build and send the trailer
                BuildAndSendSimpleCommand(HcomMeadowRequestType.HCOM_MDOW_REQUEST_END_FILE_TRANSFER, 0);

                // bufferOffset should point to the byte after the last byte
                Debug.Assert(fileBufOffset == fileBytes.Length);
                Console.WriteLine("Total bytes sent {0:N0} in {1:N0} packets. PacketCRC:{2:x08} MaxPacket:{3}",
                    fileBufOffset, sequenceNumber, _packetCrc32, maxSizeOfXmitPacket);
            }
            catch (Exception except)
            {
                Console.WriteLine("Exception sending:{0}", except);
            }
        }

        //==========================================================================
        internal void SendSimpleCommand(HcomMeadowRequestType requestType, uint userData = 0)
        {
            BuildAndSendSimpleCommand(requestType, userData);
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
                byte[] encodedBytes = new byte[maxSizeOfXmitPacket];

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
        // Build and send a basic command
        internal void BuildAndSendSimpleCommand(HcomMeadowRequestType requestType, UInt32 userData)
        {
            var messageBytes = new byte[HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH];

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

            // Protocol control (future)
            Array.Copy(BitConverter.GetBytes((UInt16)HCOM_PROTOCOL_CONTROL_VALUE_FUTURE), 0, messageBytes, offset, sizeof(UInt16));
            offset += sizeof(UInt16);


            // Command type (2 bytes)
            Array.Copy(BitConverter.GetBytes((UInt16)requestType), 0, messageBytes, offset, sizeof(UInt16));
            offset += sizeof(UInt16);

            // User Data
            Array.Copy(BitConverter.GetBytes((UInt32)userData), 0, messageBytes, offset, sizeof(UInt32));
            offset += sizeof(UInt32);

            return offset;
        }

        //==========================================================================
        internal void BuildAndSendFileRelatedCommand(HcomMeadowRequestType requestType,
            UInt32 userData, UInt32 fileSize, UInt32 fileCheckSum, string destFileName)
        {
            // Future: Try to use the StructLayout attribute to build fixed size class/struct.

            // Allocate the correctly size message buffer
            byte[] targetFileName = Encoding.UTF8.GetBytes(destFileName);           // Using UTF-8 works for ASCII but should be Unicode in nuttx
            int optionalDataLength = sizeof(UInt32) + sizeof(UInt32) + targetFileName.Length;
            byte[] messageBytes = new byte[HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH + optionalDataLength];

            // Add the required part
            int offset = BuildMeadowBoundSimpleCommand(requestType, userData, ref messageBytes);

            // File Size
            Array.Copy(BitConverter.GetBytes(fileSize), 0, messageBytes, offset, sizeof(UInt32));
            offset += sizeof(UInt32);

            // CRC32 checksum or delete file partition number
            Array.Copy(BitConverter.GetBytes(fileCheckSum), 0, messageBytes, offset, sizeof(UInt32));
            offset += sizeof(UInt32);

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

                byte[] encodedBytes = new byte[maxSizeOfXmitPacket];
                int encodedToSend = CobsTools.CobsEncoding(messageBytes, messageOffset, messageSize, ref encodedBytes);

                // Verify COBS - any delimiters left?
                for (int i = 0; i < encodedToSend; i++)
                {
                    if (encodedBytes[i] == 0x00)
                    {
                        throw new InvalidProgramException("All zeros should have been removed. " +
                            $"There's one at {i}");
                    }
                }

                // Terminate packet with delimiter so packet boundaries can be found
                encodedBytes[encodedToSend] = 0;
                encodedToSend++;

                try
                {
                    _serialPort.Write(encodedBytes, 0, encodedToSend);
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
                Console.WriteLine($"An exception was caught: {except}");
                throw;
            }
        }
    }
}