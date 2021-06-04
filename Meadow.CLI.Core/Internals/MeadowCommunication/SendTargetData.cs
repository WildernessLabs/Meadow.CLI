using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.DeviceManagement.Tools;
using Meadow.CLI.Core.Exceptions;

using MeadowCLI;

using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Core.Internals.MeadowCommunication
{
    public class SendTargetData
    {
        const int HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH = 12;
        const int HCOM_PROTOCOL_REQUEST_MD5_HASH_LENGTH = 32;
        const int HCOM_PROTOCOL_COMMAND_SEQ_NUMBER = 0;
        const ushort HCOM_PROTOCOL_EXTRA_DATA_DEFAULT_VALUE = 0x0000;       // Currently not used field

        //questioning if this class should send or just create the message
        private readonly MeadowLocalDevice _device; //refactor this .... 
        private readonly ILogger _logger;

        uint _packetCrc32;

        //==========================================================================
        // Constructor
        public SendTargetData(MeadowLocalDevice device, ILogger logger)
        {
            _device = device;
            _logger = logger;
        }

        public async Task SendTheEntireFile(HcomMeadowRequestType requestType, string destFileName,
            uint partitionId, byte[] fileBytes, uint mcuAddress, uint payloadCrc32,
            string md5Hash, bool lastInSeries, CancellationToken cancellationToken)
        {
            _packetCrc32 = 0;

            try
            {
                //--------------------------------------------------------------
                int responseWaitTime;
                if (requestType == HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER)
                {
                    _logger.LogDebug("Erasing ESP32 Flash...");
                    responseWaitTime = 30_000;
                }
                else
                {
                    responseWaitTime = 10_000;
                }

                // Build and send the header
                await BuildAndSendFileRelatedCommand(requestType,
                    partitionId, (uint)fileBytes.Length, payloadCrc32,
                    mcuAddress, md5Hash, destFileName, cancellationToken);

                //==== Wait for response from Meadow
                // create our message filter.
                Predicate<MeadowMessageEventArgs> filter = p => (
                    p.MessageType == MeadowMessageType.Concluded ||
                    p.MessageType == MeadowMessageType.DownloadStartOkay ||
                    p.MessageType == MeadowMessageType.DownloadStartFail);
                // await the response

                var (success, _, messageType) = await _device.WaitForResponseMessageAsync(filter, responseWaitTime, cancellationToken)
                    .ConfigureAwait(false);

                // if it failed, bail out
                if (!success)
                {
                    _logger.LogDebug("Message response indicates failure");
                    return;
                }

                // if it's an ESP start file transfer and the download started ok.
                if (requestType == HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER
                                   && messageType == MeadowMessageType.DownloadStartOkay)
                {
                    _logger.LogDebug("ESP32 download request accepted");
                }
                // if it's an ESP file transfer start and it failed to start
                else if (requestType == HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER &&
                                      messageType == MeadowMessageType.DownloadStartFail)
                {
                    _logger.LogDebug("ESP32 download request rejected");
                }

                // if the download didn't start ok.
                if (messageType != MeadowMessageType.DownloadStartOkay)
                {
                    throw messageType switch
                    {
                        MeadowMessageType.DownloadStartFail => new MeadowCommandException(
                            "Halting download due to an error while preparing Meadow for download"),
                        MeadowMessageType.Concluded => new MeadowCommandException(
                            "Halting download due to an unexpectedly Meadow 'Concluded' received prematurely"),
                        _ => new MeadowCommandException(
                            $"Halting download due to an unexpected Meadow message type {messageType} received")
                    };
                }

                // Build each data packet
                var fileBufOffset = 0;
                int numBytesToSend;
                ushort sequenceNumber = 1;

                // don't echo the device responses
                //_device.LocalEcho = false;

                WriteProgress(-1);
                while (fileBufOffset <= fileBytes.Length - 1)           // equal would mean past the end
                {
                    if ((fileBufOffset + MeadowDeviceManager.MaxAllowableMsgPacketLength) > (fileBytes.Length - 1))
                    {
                        numBytesToSend = fileBytes.Length - fileBufOffset;  // almost done, last packet
                    }
                    else
                    {
                        numBytesToSend = MeadowDeviceManager.MaxAllowableMsgPacketLength;
                    }

                    await BuildAndSendDataPacketRequest(fileBytes, fileBufOffset, numBytesToSend, sequenceNumber, cancellationToken).ConfigureAwait(false);

                    var progress = fileBufOffset * 100 / fileBytes.Length;
                    WriteProgress(progress);

                    fileBufOffset += numBytesToSend;

                    sequenceNumber++;
                }
                WriteProgress(101);

                // echo the device responses
                await Task.Delay(250, cancellationToken); // if we're too fast, we'll finish and the device will still echo a little

                //--------------------------------------------------------------
                // Build and send the correct trailer
                switch (requestType)
                {
                    // Provide the correct message end depending on the reason the file
                    // is being downloaded to the F7 file system.
                    case HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER:
                        await BuildAndSendSimpleCommand(HcomMeadowRequestType.HCOM_MDOW_REQUEST_END_FILE_TRANSFER,
                                                        lastInSeries ? (uint)1 : (uint)0, cancellationToken).ConfigureAwait(false);      // set UserData
                        break;
                    case HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_UPDATE_RUNTIME:
                        await BuildAndSendSimpleCommand(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_UPDATE_FILE_END,
                                                        lastInSeries ? (uint)1 : (uint)0, cancellationToken).ConfigureAwait(false);      // set UserData
                        break;
                    case HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER:
                        await BuildAndSendSimpleCommand(HcomMeadowRequestType.HCOM_MDOW_REQUEST_END_ESP_FILE_TRANSFER,
                                                        lastInSeries ? (uint)1 : (uint)0, cancellationToken).ConfigureAwait(false);      // set UserData
                        break;
                    default:
                        Console.WriteLine($"File end Meadow request type of {requestType} not defined");
                        break;
                }

                // bufferOffset should point to the byte after the last byte
                Debug.Assert(fileBufOffset == fileBytes.Length);
                _logger.LogTrace($"Total bytes sent {fileBufOffset:N0} in {sequenceNumber:N0} packets. PacketCRC:{_packetCrc32:x08}");
            }
            catch (Exception except)
            {
                _logger.LogError(except, "Exception sending command to Meadow");
                throw;
            }
        }

        // TODO: Make sure this doesn't mess up other logging
        private void WriteProgress(int i)
        {
            // 50 characters - 2% each
            if (i < 0)
            {
                Console.Write($"[                                                  ]");
            }
            else if (i > 100)
            {
                Console.WriteLine($"\r[==================================================]");
            }
            else
            {
                var p = i / 2;
                //                Console.WriteLine($"{i} | {p}");
                Console.Write($"\r[{new string('=', p)}{new string(' ', 50 - p)}]");
            }
        }

        //==========================================================================
        internal async Task<bool> SendSimpleCommand(HcomMeadowRequestType requestType, uint userData = 0, bool doAcceptedCheck = true, CancellationToken cancellationToken = default)
        {
            _logger.LogTrace("Sending command {requestType}", requestType);
            var tcs = new TaskCompletionSource<bool>();
            var received = false;

            if (!doAcceptedCheck)
            {
                await BuildAndSendSimpleCommand(requestType, userData, cancellationToken).ConfigureAwait(false);
                return true;
            }

            EventHandler<MeadowMessageEventArgs> handler = (s, e) =>
            {
                _logger.LogTrace("Received MessageType: {messageType} Message: {message}", e.MessageType, string.IsNullOrWhiteSpace(e.Message) ? "[empty]" : e.Message);
                if (e.MessageType == MeadowMessageType.Accepted)
                {
                    received = true;
                    tcs.SetResult(true);
                }
            };

            _logger.LogTrace("Attaching data received handler");
            _device.DataProcessor.OnReceiveData += handler;

            await BuildAndSendSimpleCommand(requestType, userData, cancellationToken).ConfigureAwait(false);

            try
            {
                using var cts = new CancellationTokenSource(10_000);
                cts.Token.Register(() => tcs.TrySetCanceled());
                await tcs.Task.ConfigureAwait(false);
            }
            catch (TaskCanceledException e)
            {
                throw new MeadowCommandException("Command timeout waiting for response.", e);
            }
            finally
            {
                _logger.LogTrace("Removing data received handler");
                _device.DataProcessor.OnReceiveData -= handler;
            }

            if (!received)
            {
                throw new MeadowCommandException("Command not accepted.");
            }
            return received;
        }

        //==========================================================================
        // Prepare a data packet for sending
        private async Task BuildAndSendDataPacketRequest(byte[] messageBytes, int messageOffset,
            int messageSize, ushort sequenceNumber, CancellationToken cancellationToken)
        {
            try
            {
                // Need to prepend the sequence number to the packet
                int xmitSize = messageSize + sizeof(ushort);
                byte[] fullMsg = new byte[xmitSize];

                byte[] seqBytes = BitConverter.GetBytes(sequenceNumber);
                Array.Copy(seqBytes, fullMsg, sizeof(ushort));
                Array.Copy(messageBytes, messageOffset, fullMsg, sizeof(ushort), messageSize);

                await EncodeAndSendPacket(fullMsg, 0, xmitSize, cancellationToken).ConfigureAwait(false);
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
        internal async Task BuildAndSendSimpleData(byte[] additionalData, HcomMeadowRequestType requestType, uint userData, CancellationToken cancellationToken)
        {
            int totalMsgLength = additionalData.Length + HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH;
            var messageBytes = new byte[totalMsgLength];

            // Populate the header
            BuildMeadowBoundSimpleCommand(requestType, userData, ref messageBytes);

            // Copy the payload into the message
            Array.Copy(additionalData, 0, messageBytes,
                HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH, additionalData.Length);

            await EncodeAndSendPacket(messageBytes, 0, totalMsgLength, cancellationToken).ConfigureAwait(false);
        }

        //==========================================================================
        // Build and send a "simple" message with only a header
        internal async Task BuildAndSendSimpleCommand(HcomMeadowRequestType requestType, uint userData, CancellationToken cancellationToken)
        {
            var messageBytes = new byte[HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH];

            // Populate the header
            BuildMeadowBoundSimpleCommand(requestType, userData, ref messageBytes);
            await EncodeAndSendPacket(messageBytes, 0, HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH, cancellationToken).ConfigureAwait(false);
        }

        //==========================================================================
        // This is most of the mandatory part of every non-data packet
        private int BuildMeadowBoundSimpleCommand(HcomMeadowRequestType requestType,
                                                  uint userData, ref byte[] messageBytes)
        {
            // Note: Could use the StructLayout attribute to build
            int offset = 0;

            // Two byte seq numb
            Array.Copy(BitConverter.GetBytes((ushort)HCOM_PROTOCOL_COMMAND_SEQ_NUMBER), 0,
                messageBytes, offset, sizeof(ushort));
            offset += sizeof(ushort);

            // Protocol version
            Array.Copy(BitConverter.GetBytes(Constants.HCOM_PROTOCOL_CURRENT_VERSION_NUMBER), 0, messageBytes, offset, sizeof(ushort));
            offset += sizeof(ushort);

            // Command type (2 bytes)
            Array.Copy(BitConverter.GetBytes((ushort)requestType), 0, messageBytes, offset, sizeof(ushort));
            offset += sizeof(ushort);

            // Extra Data
            Array.Copy(BitConverter.GetBytes((ushort)HCOM_PROTOCOL_EXTRA_DATA_DEFAULT_VALUE), 0, messageBytes, offset, sizeof(ushort));
            offset += sizeof(ushort);

            // User Data
            Array.Copy(BitConverter.GetBytes((uint)userData), 0, messageBytes, offset, sizeof(uint));
            offset += sizeof(uint);

            return offset;
        }

        //==========================================================================
        internal async Task BuildAndSendFileRelatedCommand(
            HcomMeadowRequestType requestType, uint userData, uint fileSize, uint fileCheckSum, 
            uint mcuAddress, string md5Hash, string destFileName, CancellationToken cancellationToken)
        {
            _logger.LogTrace("Building {requestType} command", requestType);
            // Future: Try to use the StructLayout attribute
            Debug.Assert(md5Hash.Length == 0 || md5Hash.Length == HCOM_PROTOCOL_REQUEST_MD5_HASH_LENGTH);

            // Allocate the correctly size message buffers
            byte[] targetFileName = Encoding.UTF8.GetBytes(destFileName);           // Using UTF-8 works for ASCII but should be Unicode in nuttx
            byte[] md5HashBytes = Encoding.UTF8.GetBytes(md5Hash);
            int optionalDataLength = sizeof(uint) + sizeof(uint) + sizeof(uint) +
                HCOM_PROTOCOL_REQUEST_MD5_HASH_LENGTH + targetFileName.Length;
            byte[] messageBytes = new byte[HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH + optionalDataLength];

            // Add the required header
            int offset = BuildMeadowBoundSimpleCommand(requestType, userData, ref messageBytes);

            // File Size
            Array.Copy(BitConverter.GetBytes(fileSize), 0, messageBytes, offset, sizeof(uint));
            offset += sizeof(uint);

            // CRC32 checksum or delete file partition number
            Array.Copy(BitConverter.GetBytes(fileCheckSum), 0, messageBytes, offset, sizeof(uint));
            offset += sizeof(uint);

            // MCU address for this file. Used for ESP32 file downloads
            Array.Copy(BitConverter.GetBytes(mcuAddress), 0, messageBytes, offset, sizeof(uint));
            offset += sizeof(uint);

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
            await EncodeAndSendPacket(messageBytes, 0, offset, cancellationToken).ConfigureAwait(false);
        }

        //==========================================================================
        // Last stop before transmitting information
        private async Task EncodeAndSendPacket(byte[] messageBytes, int messageOffset, int messageSize, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogTrace("Sending packet of {messageSize} bytes", messageSize);
                // For testing calculate the crc including the sequence number
                _packetCrc32 = CrcTools.Crc32part(messageBytes, messageSize, 0, _packetCrc32);
                _logger.LogTrace("Calculated Crc {crc}", _packetCrc32);

                // Add 2, first to account for start delimiter and second for end
                byte[] encodedBytes = new byte[MeadowDeviceManager.MaxEstimatedSizeOfEncodedPayload + 2];
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

                _logger.LogTrace("Encoded packet successfully");
                try
                {
                    await _device.WriteAsync(encodedBytes, encodedToSend, cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidOperationException ioe)  // Port not opened
                {
                    _logger.LogError(ioe, "Write but port not opened");
                    throw;
                }
                catch (ArgumentOutOfRangeException aore)  // offset or count don't match buffer
                {
                    _logger.LogError(aore, "Write buffer, offset and count don't line up");
                    throw;
                }
                catch (ArgumentException ae)  // offset plus count > buffer length
                {
                    _logger.LogError(ae, "Write offset plus count > buffer length");
                    throw;
                }
                catch (TimeoutException te) // Took too long to send
                {
                    _logger.LogError(te, "Write took too long to send");
                    throw;
                }
            }
            catch (Exception except)
            {
                _logger.LogTrace(except, "EncodeAndSendPacket threw");
                throw;
            }
        }
    }

    public class NotConnectedException : Exception { }
}
