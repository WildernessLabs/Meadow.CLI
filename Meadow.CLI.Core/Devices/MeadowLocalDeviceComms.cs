using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.DeviceManagement.Tools;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Internals.MeadowCommunication;

namespace Meadow.CLI.Core.Devices
{
    public partial class MeadowLocalDevice
    {
        uint _packetCrc32;
        private readonly SemaphoreSlim _comPortSemaphore = new(1, 1);

        async Task TransferFile(byte[]? fileData, int fileSize, CancellationToken cancellationToken)
        {
            if (fileData == null)
            {
                throw new Exception("No file data present while attempting to write file");
            }

            var fileBufOffset = 0;
            ushort sequenceNumber = 1;

            Logger.LogInformation("Starting File Transfer...");
            while (fileBufOffset <= fileSize - 1) // equal would mean past the end
            {
                int numBytesToSend;
                if (fileBufOffset + DataProcessor.MaxAllowableMsgPacketLength > fileSize - 1)
                {
                    numBytesToSend = fileSize - fileBufOffset; // almost done, last packet
                }
                else
                {
                    numBytesToSend = DataProcessor.MaxAllowableMsgPacketLength;
                }

                await BuildAndSendDataPacketRequest(
                        fileData,
                        fileBufOffset,
                        numBytesToSend,
                        sequenceNumber,
                        cancellationToken);

                fileBufOffset += numBytesToSend;

                sequenceNumber++;
            }

            // bufferOffset should point to the byte after the last byte
            Debug.Assert(fileBufOffset == fileSize);

            Logger.LogTrace(
                "Total bytes sent {count} in {packetCount} packets. PacketCRC:{_crc}",
                fileBufOffset,
                sequenceNumber,
                $"{_packetCrc32:x08}");

            Logger.LogInformation("Transfer Complete, wrote {count} bytes to Meadow", fileBufOffset);
        }

        /// <summary>
        /// Method to get the correct "trailer" or following command after sending a files 
        /// </summary>
        /// <param name="command">The command used to send the file</param>
        /// <param name="lastInSeries">Is this the final file if multiple files are being sent together</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        Command GetTrailerCommand(FileCommand command, bool lastInSeries)
        {
            //--------------------------------------------------------------
            // Build and send the correct trailer
            // TODO: Move this into the Command object
            return command.RequestType switch
            {
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER =>
                    new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_END_FILE_TRANSFER)
                        .WithUserData(lastInSeries ? 1U : 0U)
                        .Build(),
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_UPDATE_RUNTIME =>
                    new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_UPDATE_FILE_END)
                        .WithUserData(lastInSeries ? 1U : 0U)
                        .WithTimeout(TimeSpan.FromSeconds(60))
                        .Build(),
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER =>
                    new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_END_ESP_FILE_TRANSFER)
                        .WithUserData(lastInSeries ? 1U : 0U)
                        .Build(),
                _ => throw new ArgumentOutOfRangeException(nameof(command.RequestType), "Cannot build trailer for unknown command")
            };
        }

        /// <summary>
        /// Validate the send command and response when sending a file to Meadow
        /// This should really be split up across distinct methods .. but one step at a time
        /// </summary>
        /// <exception cref="MeadowCommandException"></exception>
        void ValidateSendCommandAndResponse(Command command, CommandResponse response)
        {
            if (response.MessageType == MeadowMessageType.DownloadStartFail)
            {
                throw new MeadowCommandException(command, $"Meadow rejected download request with {response.Message}", response);
            }

            switch (command.RequestType)
            {
                // if it's an ESP start file transfer and the download started ok.
                case HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER
                        when response.MessageType == MeadowMessageType.DownloadStartOkay:
                    Logger.LogDebug("ESP32 download request accepted");
                    break;
                // if it's an ESP file transfer start and it failed to start
                case HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER
                        when response.MessageType == MeadowMessageType.DownloadStartFail:
                    Logger.LogDebug("ESP32 download request rejected");
                    throw new MeadowCommandException(command,
                                                     "Halting download due to an error while preparing Meadow for download",
                                                     response);
                case HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER
                        when response.MessageType != MeadowMessageType.DownloadStartOkay:
                    throw response.MessageType switch
                    {
                        MeadowMessageType.DownloadStartFail => new MeadowCommandException(command,
                            "Halting download due to an error while preparing Meadow for download",
                            response),
                        MeadowMessageType.Concluded => new MeadowCommandException(command,
                            "Halting download due to an unexpectedly Meadow 'Concluded' received prematurely",
                            response),
                        _ => new MeadowCommandException(command,
                                                        $"Halting download due to an unexpected Meadow message type {response.MessageType} received",
                                                        response)
                    };
            }
        }

        public async Task SendFileAndTrailerCommand(FileCommand command,
                                            bool lastInSeries,
                                            CancellationToken cancellationToken)
        {
            _packetCrc32 = 0;

            Logger.LogDebug("Sending {filename} to device", command.DestinationFileName);
            try
            {
                var response = await SendCommand(command, cancellationToken);

                ValidateSendCommandAndResponse(command, response);

                await TransferFile(command.FileBytes, command.FileSize, cancellationToken);

                // Build and send the correct trailer command - i.e. the command that should follow the file write 
                var trailerCommand = GetTrailerCommand(command, lastInSeries);
                await SendCommand(trailerCommand, cancellationToken);
            }
            catch (Exception except)
            {
                Logger.LogError(except, "Exception sending command to Meadow");
                throw;
            }
        }

        private async Task BuildAndSendDataPacketRequest(byte[] messageBytes,
                                                         int messageOffset,
                                                         int messageSize,
                                                         ushort sequenceNumber,
                                                         CancellationToken cancellationToken)
        {
            try
            {
                // Need to prepend the sequence number to the packet
                var transmitSize = messageSize + sizeof(ushort);
                byte[] fullMsg = new byte[transmitSize];

                byte[] seqBytes = BitConverter.GetBytes(sequenceNumber);
                Array.Copy(seqBytes, fullMsg, sizeof(ushort));
                Array.Copy(messageBytes, messageOffset, fullMsg, sizeof(ushort), messageSize);

                await EncodeAndSendPacket(fullMsg, 0, transmitSize, cancellationToken);
            }
            catch (Exception except)
            {
                Console.WriteLine($"An exception was caught: {except}");
                throw;
            }
        }

        private protected async Task<CommandResponse> SendCommand(
            Command command,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string? caller = null)
        {
            await _comPortSemaphore.WaitAsync(cancellationToken);

            try
            {
                Logger.LogTrace($"{caller} is sending {command.RequestType}");

                CommandResponse resp;
                if (command.IsAcknowledged)
                {
                    resp = await WaitForResponseMessageAsync(command, cancellationToken);
                }
                else
                {
                    var messageBytes = command.ToMessageBytes();
                    await EncodeAndSendPacket(messageBytes, 0, messageBytes.Length, cancellationToken);
                    resp = CommandResponse.Empty;
                }

                Logger.LogTrace(
                    "Returning to {caller} with {success} {message}",
                    caller,
                    resp.IsSuccess,
                    string.IsNullOrWhiteSpace(resp.Message) ? "[empty]" : resp.Message);

                return resp;
            }
            finally
            {
                _comPortSemaphore.Release();
            }
        }
        
        private async Task EncodeAndSendPacket(byte[] messageBytes,
                                               int messageOffset,
                                               int messageSize,
                                               CancellationToken cancellationToken)
        {
            try
            {
                // For testing calculate the crc including the sequence number
                _packetCrc32 = CrcTools.Crc32part(messageBytes, messageSize, 0, _packetCrc32);

                // Add 2, first to account for start delimiter and second for end
                byte[] encodedBytes = new byte[DataProcessor.MaxEstimatedSizeOfEncodedPayload + 2];

                // Skip first byte so it can be a start delimiter
                int encodedToSend = CobsTools.CobsEncoding(
                    messageBytes,
                    messageOffset,
                    messageSize,
                    ref encodedBytes,
                    1);

                // Verify COBS - any delimiters left? Skip first byte
                for (int i = 1; i < encodedToSend; i++)
                {
                    if (encodedBytes[i] == 0x00)
                    {
                        throw new InvalidProgramException($"All zeros should have been removed. There's one at offset of {i}");
                    }
                }

                // Terminate packet with delimiter so packet boundaries can be more easily found
                encodedBytes[0] = 0; // Start delimiter
                encodedToSend++;
                encodedBytes[encodedToSend] = 0; // End delimiter
                encodedToSend++;

                try
                {
                    using var cts = new CancellationTokenSource(DefaultTimeout);
                    cts.Token.Register(() => throw new TimeoutException("Timeout while writing to serial port"));
                    var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
                    await Write(encodedBytes, encodedToSend, combinedCts.Token);
                }
                catch (InvalidOperationException ioe) // Port not opened
                {
                    Logger.LogError(ioe, "Write but port not opened");
                    throw;
                }
                catch (ArgumentOutOfRangeException aore) // offset or count don't match buffer
                {
                    Logger.LogError(aore, "Write buffer, offset and count don't line up");
                    throw;
                }
                catch (ArgumentException ae) // offset plus count > buffer length
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

        private protected async Task<CommandResponse> WaitForResponseMessageAsync(Command command,
                                        CancellationToken cancellationToken = default,
                                        [CallerMemberName] string? caller = null)
        {
            Logger.LogTrace(
                "{caller} is waiting {seconds} for response to {requestType}.",
                caller,
                command.Timeout.TotalSeconds,
                command.RequestType);

            var tcs = new TaskCompletionSource<bool>();
            var result = false;
            var message = string.Empty;
            var messageType = MeadowMessageType.ErrOutput;

            void ResponseHandler(object s, MeadowMessageEventArgs e)
            {
                Logger.LogTrace(
                    "Received MessageType: {messageType} Message: {message}",
                    e.MessageType,
                    string.IsNullOrWhiteSpace(e.Message) ? "[empty]" : e.Message);

                if (command.ResponsePredicate(e))
                {
                    Logger.LogTrace("Message matched response filter");
                    message = e.Message;
                    messageType = e.MessageType;
                    result = true;
                }

                if (command.CompletionPredicate(e))
                {
                    Logger.LogTrace("Setting result complete");
                    //message = e.Message;
                    //messageType = e.MessageType;
                    result = true; //TODO: Adrian - Pete - should this be here?? I added it
                    tcs.SetResult(true);
                }
            }

            Logger.LogTrace("Attaching response handler(s)");

            Debug.Assert(DataProcessor != null);
            if (command.ResponseHandler != null)
            {
                DataProcessor.OnReceiveData += command.ResponseHandler;
            }

            DataProcessor.OnReceiveData += ResponseHandler;
            Logger.LogTrace("Attaching completion handler(s)");

            try
            {
                var messageBytes = command.ToMessageBytes();
                await EncodeAndSendPacket(messageBytes, 0, messageBytes.Length, cancellationToken);

                using var timeoutCancellationTokenSource =
                    new CancellationTokenSource(command.Timeout);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellationTokenSource.Token);

                timeoutCancellationTokenSource.Token.Register(() => tcs.TrySetCanceled());
                await tcs.Task;
                if (cts.IsCancellationRequested)
                {
                    throw new TimeoutException("Timeout while waiting for meadow");
                }
            }
            catch (TaskCanceledException e)
            {
                throw new MeadowCommandException(command,
                                                 "Command timeout waiting for response.",
                                                 innerException: e);
            }
            finally
            {
                Logger.LogTrace("Removing handlers");
                DataProcessor.OnReceiveData -= ResponseHandler;

                if (command.ResponseHandler != null)
                {
                    DataProcessor.OnReceiveData -= command.ResponseHandler;
                }
            }

            if (result)
            {
                Logger.LogTrace(
                    $"Returning to {caller} with {message}",
                    caller,
                    string.IsNullOrWhiteSpace(message) ? "[empty]" : message);

                return new CommandResponse(result, message, messageType);
            }

            throw new MeadowCommandException(command, message);
        }
    }
}