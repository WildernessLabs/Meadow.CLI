﻿using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.DeviceManagement.Tools;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Internals.MeadowCommunication;
using Meadow.Hcom;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.Devices
{
    public partial class MeadowLocalDevice
    {
        private const int PROGESS_INCREMENTS = 5;
        uint _packetCrc32;
        private readonly SemaphoreSlim _comPortSemaphore = new SemaphoreSlim(1, 1);
        bool reUploadSkippedFiles = false;
        byte reUploadCounter = 0;

        public async Task SendTheEntireFile(FileCommand command,
                                            bool lastInSeries,
                                            CancellationToken cancellationToken)
        {
            _packetCrc32 = 0;

            try
            {
                var response = await SendCommand(command, cancellationToken);

                string responseMessage = string.Empty;
                if (response.MessageType == MeadowMessageType.DownloadStartFail)
                {
                    if (!string.IsNullOrEmpty(response.Message))
                    {
                        throw new MeadowCommandException(command, $"Meadow rejected download request with the message: {responseMessage}", response);
                    }
                    throw new MeadowCommandException(command, $"Meadow rejected download request with an empty response message (DownloadStartFail)", response);
                }

                switch (command.RequestType)
                {
                    // if it's an ESP start file transfer and the download started ok.
                    case HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER
                        when response.MessageType == MeadowMessageType.DownloadStartOkay:
                        Logger?.LogDebug("ESP32 download request accepted");
                        break;
                    // if it's an ESP file transfer start and it failed to start
                    case HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER
                        when response.MessageType == MeadowMessageType.DownloadStartFail:
                        Logger?.LogDebug("ESP32 download request rejected");
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

                var fileBufOffset = 0;
                ushort sequenceNumber = 1;

                Logger?.LogInformation($"Starting File Transfer... {Environment.NewLine} ");

                if (!InMeadowCLI) // In separate call as used for progress delimiter
                {
                    Logger?.LogInformation("[");
                }

                nextProgress = 0;
                while (fileBufOffset <= command.FileSize - 1) // equal would mean past the end
                {
                    int numBytesToSend;
                    if (fileBufOffset + MeadowDeviceManager.MaxAllowableMsgPacketLength
                      > command.FileSize - 1)
                    {
                        numBytesToSend =
                            command.FileSize - fileBufOffset; // almost done, last packet
                    }
                    else
                    {
                        numBytesToSend = MeadowDeviceManager.MaxAllowableMsgPacketLength;
                    }

                    if (command.FileBytes == null)
                    {
                        throw new MeadowCommandException(command, "File bytes are missing for file command");
                    }

                    await BuildAndSendDataPacketRequest(
                            command.FileBytes,
                            fileBufOffset,
                            numBytesToSend,
                            sequenceNumber,
                            cancellationToken);

                    var progress = (decimal)fileBufOffset / command.FileSize;
                    WriteProgress(progress);

                    fileBufOffset += numBytesToSend;

                    sequenceNumber++;
                }

                // echo the device responses
                //await Task.Delay(250, cancellationToken); // if we're too fast, we'll finish and the device will still echo a little

                //--------------------------------------------------------------
                // Build and send the correct trailer
                // TODO: Move this into the Command object
                var trailerCommand = command.RequestType switch
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
                            .WithTimeout(TimeSpan.FromSeconds(180))
                            .WithUserData(lastInSeries ? 1U : 0U)
                            .Build(),
                    _ => throw new ArgumentOutOfRangeException(
                             nameof(command.RequestType),
                             "Cannot build trailer for unknown command")
                };

                await SendCommand(trailerCommand, cancellationToken);


                // bufferOffset should point to the byte after the last byte
                Debug.Assert(fileBufOffset == command.FileSize);
                Logger?.LogTrace(
                    "Total bytes sent {count} in {packetCount} packets. PacketCRC:{_crc}",
                    fileBufOffset,
                    sequenceNumber,
                    $"{_packetCrc32:x08}");

                if (!InMeadowCLI) // In separate call as used for progress delimiter
                {
                    Logger?.LogInformation("]");
                }
                Logger?.LogInformation($"{Environment.NewLine}Transfer Complete, wrote {fileBufOffset} bytes to Meadow" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Logger?.LogError($"{ex.Message}");
                throw;
            }
        }

        private int nextProgress;

        private void WriteProgress(decimal i)
        {
            var intProgress = Convert.ToInt32(i * 100);

            if (intProgress > nextProgress)
            {
                if (!InMeadowCLI) // In separate call as used for progress delimiter
                {
                    Logger?.LogInformation("=");
                }
                nextProgress += PROGESS_INCREMENTS;
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
                Logger?.LogTrace($"{caller} is sending {command.RequestType}");

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

                Logger?.LogTrace(
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
                byte[] encodedBytes =
                    new byte[MeadowDeviceManager.MaxEstimatedSizeOfEncodedPayload + 2];

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
                        throw new InvalidProgramException(
                            "All zeros should have been removed. There's one at offset of {i}");
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
                    Logger?.LogError(ioe, $"Write but port not opened{Environment.NewLine}");
                    throw;
                }
                catch (ArgumentOutOfRangeException aore) // offset or count don't match buffer
                {
                    Logger?.LogError(aore, $"Write buffer, offset and count don't line up{Environment.NewLine}");
                    throw;
                }
                catch (ArgumentException ae) // offset plus count > buffer length
                {
                    Logger?.LogError(ae, $"Write offset plus count > buffer length{Environment.NewLine}");
                    throw;
                }
                catch (TimeoutException te) // Took too long to send
                {
                    Logger?.LogError(te, $"Write took too long to send {Environment.NewLine}");
                    throw;
                }
            }
            catch (Exception except)
            {
                Logger?.LogTrace(except, $"EncodeAndSendPacket threw{Environment.NewLine}");
                throw;
            }
        }

        private protected async Task<CommandResponse> WaitForResponseMessageAsync(Command command,
                                        CancellationToken cancellationToken = default,
                                        [CallerMemberName] string? caller = null)
        {
            Logger?.LogTrace(
                "{caller} is waiting {seconds} for response to {requestType}.",
                caller,
                command.Timeout.TotalSeconds,
                command.RequestType);

            var tcs = new TaskCompletionSource<bool>();
            var result = false;
            var message = string.Empty;
            var messageType = MeadowMessageType.ErrOutput;

            void ResponseHandler(object? s, MeadowMessageEventArgs e)
            {

                var msg = string.IsNullOrWhiteSpace(e.Message) ? "[empty]" : e.Message;

                switch (e.MessageType)
                {
                    case MeadowMessageType.Data:
                        Logger?.LogDebug(msg); // We may not need this
                        break;
                    case MeadowMessageType.ErrOutput:
                        if (msg.ToLower().Contains("newer cli protocol version"))
                        {
                            // Parse message for required version
                            var msgSplit = msg.ToLower().Split(':');
                            if (msgSplit.Length > 2)
                            {
                                int requiredVersion;
                                if (Int32.TryParse(msgSplit[3].Substring(0, 4), out requiredVersion))
                                {
                                    if (requiredVersion == Constants.HCOM_PROTOCOL_PREVIOUS_VERSION_NUMBER)
                                    {
                                        MeadowDeviceManager.MaxAllowableMsgPacketLength = MeadowDeviceManager.PreviousMaxAllowableMsgPacketLength;
                                        Command.HcomProtocolCommunicationVersion = Constants.HCOM_PROTOCOL_PREVIOUS_VERSION_NUMBER;
                                    }
                                }
                            }
                        }
                        else
                        {
                            Logger?.LogError(msg);
                        }
                        break;
                    case MeadowMessageType.DownloadFailed:
                        // Set Re-download flag, increment download count.
                        reUploadSkippedFiles = true;
                        break;
                    default:
                        break;
                }

                Logger?.LogTrace($"Received MessageType: {e.MessageType} Message: {msg}");

                if (command.ResponsePredicate(e))
                {
                    Logger?.LogTrace("Message matched response filter");
                    message = e.Message;
                    messageType = e.MessageType;
                    result = true;
                }

                if (command.CompletionPredicate(e))
                {
                    Logger?.LogTrace("Setting result complete");
                    //message = e.Message;
                    //messageType = e.MessageType;
                    result = true; //TODO: Adrian - Pete - should this be here?? I added it
                    tcs.SetResult(true);
                }
            }

            Logger?.LogTrace("Attaching response handler(s)");
            Debug.Assert(DataProcessor != null);
            if (command.ResponseHandler != null)
            {
                DataProcessor.OnReceiveData += command.ResponseHandler;
            }

            DataProcessor.OnReceiveData += ResponseHandler;
            Logger?.LogTrace("Attaching completion handler(s)");

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
                    throw new TimeoutException("Timeout while waiting for meadow");
            }
            catch (TaskCanceledException e)
            {
                throw new MeadowCommandException(command,
                                                 "Command timeout waiting for response.",
                                                 innerException: e);
            }
            finally
            {
                Logger?.LogTrace("Removing handlers");
                DataProcessor.OnReceiveData -= ResponseHandler;
                if (command.ResponseHandler != null)
                {
                    DataProcessor.OnReceiveData -= command.ResponseHandler;
                }
            }

            if (result)
            {
                Logger?.LogTrace(
                    "Returning to {caller} with {message}",
                    caller,
                    string.IsNullOrWhiteSpace(message) ? "[empty]" : message);

                return new CommandResponse(result, message, messageType);
            }

            throw new MeadowCommandException(command, message);
        }
    }
}
