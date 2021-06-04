using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Meadow.CLI.Core.DeviceManagement.Tools;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Internals.MeadowCommunication;

using MeadowCLI;

using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Core.DeviceManagement
{
    public partial class MeadowLocalDevice
    {
        private const int HcomProtocolCommandRequiredHeaderLength = 12;
        private const int HcomProtocolRequestMd5HashLength = 32;
        private const int HcomProtocolCommandSeqNumber = 0;
        private const ushort HcomProtocolExtraDataDefaultValue = 0x0000;

        uint _packetCrc32;

        public async Task SendTheEntireFile(HcomMeadowRequestType requestType, string destFileName,
                                            uint partitionId, byte[] fileBytes, uint mcuAddress, uint payloadCrc32,
                                            string md5Hash, bool lastInSeries, CancellationToken cancellationToken)
        {
            _packetCrc32 = 0;

            Logger.LogDebug("Sending {filename} to device", destFileName);
            try
            {
                //--------------------------------------------------------------
                int responseWaitTime;
                if (requestType == HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER)
                {
                    Logger.LogDebug("Erasing ESP32 Flash...");
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

                var (success, _, messageType) = await WaitForResponseMessageAsync(filter, responseWaitTime, cancellationToken)
                    .ConfigureAwait(false);

                // if it failed, bail out
                if (!success)
                {
                    Logger.LogDebug("Message response indicates failure");
                    return;
                }

                // if it's an ESP start file transfer and the download started ok.
                if (requestType == HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER
                                   && messageType == MeadowMessageType.DownloadStartOkay)
                {
                    Logger.LogDebug("ESP32 download request accepted");
                }
                // if it's an ESP file transfer start and it failed to start
                else if (requestType == HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER &&
                                      messageType == MeadowMessageType.DownloadStartFail)
                {
                    Logger.LogDebug("ESP32 download request rejected");
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

                var fileBufOffset = 0;
                ushort sequenceNumber = 1;

                Logger.LogInformation("Starting Transfer");
                while (fileBufOffset <= fileBytes.Length - 1)           // equal would mean past the end
                {
                    int numBytesToSend;
                    if ((fileBufOffset + MeadowDeviceManager.MaxAllowableMsgPacketLength) > (fileBytes.Length - 1))
                    {
                        numBytesToSend = fileBytes.Length - fileBufOffset;  // almost done, last packet
                    }
                    else
                    {
                        numBytesToSend = MeadowDeviceManager.MaxAllowableMsgPacketLength;
                    }

                    await BuildAndSendDataPacketRequest(fileBytes, fileBufOffset, numBytesToSend, sequenceNumber, cancellationToken).ConfigureAwait(false);

                    var progress = (decimal)fileBufOffset / fileBytes.Length;
                    WriteProgress(progress);

                    fileBufOffset += numBytesToSend;

                    sequenceNumber++;
                }

                // echo the device responses
                //await Task.Delay(250, cancellationToken); // if we're too fast, we'll finish and the device will still echo a little

                //--------------------------------------------------------------
                // Build and send the correct trailer
                switch (requestType)
                {
                    // Provide the correct message end depending on the reason the file
                    // is being downloaded to the F7 file system.
                    case HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER:
                        await BuildAndSendSimpleCommand(HcomMeadowRequestType.HCOM_MDOW_REQUEST_END_FILE_TRANSFER,
                                                        lastInSeries ? 1U : 0U, cancellationToken).ConfigureAwait(false);      // set UserData
                        break;
                    case HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_UPDATE_RUNTIME:
                        await BuildAndSendSimpleCommand(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_UPDATE_FILE_END,
                                                        lastInSeries ? 1U : 0U, cancellationToken).ConfigureAwait(false);      // set UserData
                        break;
                    case HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER:
                        await BuildAndSendSimpleCommand(HcomMeadowRequestType.HCOM_MDOW_REQUEST_END_ESP_FILE_TRANSFER,
                                                        lastInSeries ? 1U : 0U, cancellationToken).ConfigureAwait(false);      // set UserData
                        break;
                    default:
                        Console.WriteLine($"File end Meadow request type of {requestType} not defined");
                        break;
                }

                // bufferOffset should point to the byte after the last byte
                Debug.Assert(fileBufOffset == fileBytes.Length);
                Logger.LogTrace($"Total bytes sent {fileBufOffset:N0} in {sequenceNumber:N0} packets. PacketCRC:{_packetCrc32:x08}");
                Logger.LogInformation("Transfer Complete");
            }
            catch (Exception except)
            {
                Logger.LogError(except, "Exception sending command to Meadow");
                throw;
            }
        }

        private int _lastProgress = 0;
        private void WriteProgress(decimal i)
        {
            var intProgress = Convert.ToInt32(i * 100);
            if (intProgress <= _lastProgress || intProgress % 5 != 0) return;

            Logger.LogInformation("Operation Progress: {progress:P0}", i);
            _lastProgress = intProgress;
        }

        internal async Task SendUnacknowledgedSimpleCommand(
            HcomMeadowRequestType requestType,
            uint userData = 0,
            CancellationToken cancellationToken = default)
        {
            Logger.LogTrace("Sending command {requestType}", requestType);

            await BuildAndSendSimpleCommand(requestType, userData, cancellationToken).ConfigureAwait(false);
        }

        internal async Task<bool> SendAcknowledgedSimpleCommand(HcomMeadowRequestType requestType,
                                                           uint userData = 0,
                                                           CancellationToken cancellationToken =
                                                               default)
        {
            Logger.LogTrace("Sending command {requestType}", requestType);
            var tcs = new TaskCompletionSource<bool>();
            var received = false;
            EventHandler<MeadowMessageEventArgs> handler = (s, e) =>
            {
                Logger.LogTrace("Received MessageType: {messageType} Message: {message}", e.MessageType, string.IsNullOrWhiteSpace(e.Message) ? "[empty]" : e.Message);
                if (e.MessageType != MeadowMessageType.Accepted) return;

                received = true;
                tcs.SetResult(true);
            };

            Logger.LogTrace("Attaching data received handler");
            DataProcessor.OnReceiveData += handler;

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
                Logger.LogTrace("Removing data received handler");
                DataProcessor.OnReceiveData -= handler;
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
                var transmitSize = messageSize + sizeof(ushort);
                byte[] fullMsg = new byte[transmitSize];

                byte[] seqBytes = BitConverter.GetBytes(sequenceNumber);
                Array.Copy(seqBytes, fullMsg, sizeof(ushort));
                Array.Copy(messageBytes, messageOffset, fullMsg, sizeof(ushort), messageSize);

                await EncodeAndSendPacket(fullMsg, 0, transmitSize, cancellationToken).ConfigureAwait(false);
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
            int totalMsgLength = additionalData.Length + HcomProtocolCommandRequiredHeaderLength;
            var messageBytes = new byte[totalMsgLength];

            // Populate the header
            BuildMeadowBoundSimpleCommand(requestType, userData, ref messageBytes);

            // Copy the payload into the message
            Array.Copy(additionalData, 0, messageBytes,
                HcomProtocolCommandRequiredHeaderLength, additionalData.Length);

            await EncodeAndSendPacket(messageBytes, 0, totalMsgLength, cancellationToken).ConfigureAwait(false);
        }

        //==========================================================================
        // Build and send a "simple" message with only a header
        internal async Task BuildAndSendSimpleCommand(HcomMeadowRequestType requestType, uint userData, CancellationToken cancellationToken)
        {
            var messageBytes = new byte[HcomProtocolCommandRequiredHeaderLength];

            // Populate the header
            BuildMeadowBoundSimpleCommand(requestType, userData, ref messageBytes);
            await EncodeAndSendPacket(messageBytes, 0, HcomProtocolCommandRequiredHeaderLength, cancellationToken).ConfigureAwait(false);
        }

        //==========================================================================
        // This is most of the mandatory part of every non-data packet
        private int BuildMeadowBoundSimpleCommand(HcomMeadowRequestType requestType,
                                                  uint userData, ref byte[] messageBytes)
        {
            // Note: Could use the StructLayout attribute to build
            int offset = 0;

            // Two byte seq numb
            Array.Copy(BitConverter.GetBytes((ushort)HcomProtocolCommandSeqNumber), 0,
                messageBytes, offset, sizeof(ushort));
            offset += sizeof(ushort);

            // Protocol version
            Array.Copy(BitConverter.GetBytes(Constants.HCOM_PROTOCOL_CURRENT_VERSION_NUMBER), 0, messageBytes, offset, sizeof(ushort));
            offset += sizeof(ushort);

            // Command type (2 bytes)
            Array.Copy(BitConverter.GetBytes((ushort)requestType), 0, messageBytes, offset, sizeof(ushort));
            offset += sizeof(ushort);

            // Extra Data
            Array.Copy(BitConverter.GetBytes(HcomProtocolExtraDataDefaultValue), 0, messageBytes, offset, sizeof(ushort));
            offset += sizeof(ushort);

            // User Data
            Array.Copy(BitConverter.GetBytes(userData), 0, messageBytes, offset, sizeof(uint));
            offset += sizeof(uint);

            return offset;
        }

        //==========================================================================
        internal async Task BuildAndSendFileRelatedCommand(
            HcomMeadowRequestType requestType, uint userData, uint fileSize, uint fileCheckSum,
            uint mcuAddress, string md5Hash, string destFileName, CancellationToken cancellationToken)
        {
            Logger.LogTrace("Building {requestType} command", requestType);
            // Future: Try to use the StructLayout attribute
            Debug.Assert(md5Hash.Length == 0 || md5Hash.Length == HcomProtocolRequestMd5HashLength);

            // Allocate the correctly size message buffers
            byte[] targetFileName = Encoding.UTF8.GetBytes(destFileName);           // Using UTF-8 works for ASCII but should be Unicode in nuttx
            byte[] md5HashBytes = Encoding.UTF8.GetBytes(md5Hash);
            int optionalDataLength = sizeof(uint) + sizeof(uint) + sizeof(uint) +
                HcomProtocolRequestMd5HashLength + targetFileName.Length;
            byte[] messageBytes = new byte[HcomProtocolCommandRequiredHeaderLength + optionalDataLength];

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
                Array.Clear(messageBytes, offset, HcomProtocolRequestMd5HashLength);
            else
                Array.Copy(md5HashBytes, 0, messageBytes, offset, HcomProtocolRequestMd5HashLength);
            offset += HcomProtocolRequestMd5HashLength;

            // Destination File Name
            Array.Copy(targetFileName, 0, messageBytes, offset, targetFileName.Length);
            offset += targetFileName.Length;

            Debug.Assert(offset == optionalDataLength + HcomProtocolCommandRequiredHeaderLength);
            await EncodeAndSendPacket(messageBytes, 0, offset, cancellationToken).ConfigureAwait(false);
        }

        //==========================================================================
        // Last stop before transmitting information
        private async Task EncodeAndSendPacket(byte[] messageBytes, int messageOffset, int messageSize, CancellationToken cancellationToken)
        {
            try
            {
                Logger.LogTrace("Sending packet of {messageSize} bytes", messageSize);
                // For testing calculate the crc including the sequence number
                _packetCrc32 = CrcTools.Crc32part(messageBytes, messageSize, 0, _packetCrc32);

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

                Logger.LogTrace("Encoded packet successfully");
                try
                {
                    await WriteAsync(encodedBytes, encodedToSend, cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidOperationException ioe)  // Port not opened
                {
                    Logger.LogError(ioe, "Write but port not opened");
                    throw;
                }
                catch (ArgumentOutOfRangeException aore)  // offset or count don't match buffer
                {
                    Logger.LogError(aore, "Write buffer, offset and count don't line up");
                    throw;
                }
                catch (ArgumentException ae)  // offset plus count > buffer length
                {
                    Logger.LogError(ae, "Write offset plus count > buffer length");
                    throw;
                }
                catch (TimeoutException te) // Took too long to send
                {
                    Logger.LogError(te, "Write took too long to send");
                    throw;
                }
            }
            catch (Exception except)
            {
                Logger.LogTrace(except, "EncodeAndSendPacket threw");
                throw;
            }
        }

        private protected async Task<string?> SendCommandAndWaitForResponseAsync(HcomMeadowRequestType requestType,
            MeadowMessageType responseMessageType = MeadowMessageType.Concluded,
            uint userData = 0,
            bool doAcceptedCheck = true,
            int timeoutMs = 10000,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string? caller = null)
        {
            Logger.LogTrace("{caller} sent {requestType} waiting for {responseMessageType}", caller, requestType, responseMessageType.ToString() ?? "[empty]");
            var message = await SendCommandAndWaitForResponseAsync(
                                  requestType,
                                  e => e.MessageType == responseMessageType,
                                  userData,
                                  doAcceptedCheck,
                                  timeoutMs,
                                  cancellationToken)
                              .ConfigureAwait(false);

            Logger.LogTrace("Returning to {caller} with {message}", caller, string.IsNullOrWhiteSpace(message) ? "[empty]" : message);
            return message;
        }

        private protected async Task<string?> SendCommandAndWaitForResponseAsync(HcomMeadowRequestType requestType,
                                                             Predicate<MeadowMessageEventArgs>? filter,
                                                             uint userData = 0,
                                                             bool doAcceptedCheck = true,
                                                             int timeoutMs = 10000,
                                                             CancellationToken cancellationToken = default,
                                                             [CallerMemberName] string? caller = null)
        {

            Logger.LogTrace($"{caller} is sending {requestType}");

            await SendAcknowledgedSimpleCommand(
                                     requestType,
                                     userData,
                                     cancellationToken)
                                 .ConfigureAwait(false);

            var (isSuccess, message, _) = await WaitForResponseMessageAsync(filter, timeoutMs, cancellationToken)
                                              .ConfigureAwait(false);

            Logger.LogTrace("Returning to {caller} with {success} {message}", caller, isSuccess, string.IsNullOrWhiteSpace(message) ? "[empty]" : message);
            return message;

        }

        private protected async Task<(bool Success, string? Message, MeadowMessageType MessageType)> WaitForResponseMessageAsync(
            Predicate<MeadowMessageEventArgs>? filter,
            int millisecondDelay = 10000,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string? caller = null)
        {
            Logger.LogTrace("{caller} is waiting for response.", caller);
            if (filter == null)
            {
                return (true, string.Empty, MeadowMessageType.ErrOutput);
            }

            var tcs = new TaskCompletionSource<bool>();
            var result = false;
            var message = string.Empty;
            var messageType = MeadowMessageType.ErrOutput;

            EventHandler<MeadowMessageEventArgs> handler = (s, e) =>
            {
                Logger.LogTrace("Received MessageType: {messageType} Message: {message}", e.MessageType, string.IsNullOrWhiteSpace(e.Message) ? "[empty]" : e.Message);
                if (filter(e))
                {
                    Logger.LogTrace("Message matches filter.");
                    message = e.Message;
                    messageType = e.MessageType;
                    result = true;
                }

                var res = e.MessageType switch
                {
                    MeadowMessageType.SerialReconnect   => true,
                    MeadowMessageType.Concluded         => true,
                    MeadowMessageType.DownloadStartOkay => true,
                    MeadowMessageType.DownloadStartFail => true,
                    _                                   => false
                };

                if (res)
                {
                    tcs.SetResult(res);
                }
            };

            Logger.LogTrace("Attaching data received handler");
            DataProcessor.OnReceiveData += handler;

            try
            {
                using var cts = new CancellationTokenSource(millisecondDelay);
                cts.Token.Register(() => tcs.TrySetCanceled());
                await tcs.Task.ConfigureAwait(false);
            }
            catch (TaskCanceledException e)
            {
                throw new MeadowCommandException("Command timeout waiting for response.", e);
            }
            finally
            {
                Logger.LogTrace("Removing data received handler");
                DataProcessor.OnReceiveData -= handler;
            }

            if (result)
            {
                Logger.LogTrace("Returning to {caller} with {message}", caller, string.IsNullOrWhiteSpace(message) ? "[empty]" : message);
                return (result, message, messageType);
            }


            throw new MeadowCommandException(message);
        }
    }

    public class NotConnectedException : Exception { }
}
