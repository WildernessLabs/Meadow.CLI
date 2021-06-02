using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Internals.MeadowComms;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Core.DeviceManagement
{
    public abstract partial class MeadowLocalDevice : MeadowDevice
    {
        private readonly SendTargetData _sendTargetData;

        protected MeadowLocalDevice(MeadowDataProcessor dataProcessor, ILogger? logger = null)
            : base(dataProcessor, logger)
        {
            _sendTargetData = new SendTargetData(this, logger);
        }

        public abstract Task Write(byte[] encodedBytes, int encodedToSend);

        public abstract Task<bool> Initialize(CancellationToken cancellationToken = default);

        //device Id information is processed when the message is received
        //this will request the device Id and return true it was set successfully
        public override async Task<string?> GetDeviceInfoAsync(int timeoutMs = 5000,
                                                          CancellationToken cancellationToken =
                                                              default)
        {
            await Initialize(cancellationToken)
                .ConfigureAwait(false);

            await _sendTargetData.SendSimpleCommand(
                                     HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_DEVICE_INFORMATION,
                                     cancellationToken: cancellationToken)
                                 .ConfigureAwait(false);

            try
            {
                var (isSuccess, message, messageType) = await WaitForResponseMessage(
                               p => p.MessageType == MeadowMessageType.DeviceInfo,
                               millisecondDelay: timeoutMs,
                               cancellationToken: cancellationToken)
                           .ConfigureAwait(false);

                if (isSuccess)
                    return message;
                throw new DeviceInfoException();
            }
            catch (MeadowDeviceManagerException mdmEx)
            {
                throw new DeviceInfoException(mdmEx);
            }
        }

        //device name is processed when the message is received
        //this will request the device name and return true it was successfully
        public override async Task<string?> GetDeviceNameAsync(int timeoutMs = 5000,
                                                          CancellationToken cancellationToken =
                                                              default)
        {
            await _sendTargetData.SendSimpleCommand(
                                     HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_DEVICE_NAME,
                                     cancellationToken: cancellationToken)
                                 .ConfigureAwait(false);

            try
            {
                var (isSuccess, message, messageType) =  await WaitForResponseMessage(
                                                                 p => p.MessageType == MeadowMessageType.DeviceInfo,
                                                                 millisecondDelay: timeoutMs,
                                                                 cancellationToken: cancellationToken)
                                                             .ConfigureAwait(false);
                if (isSuccess)
                    return message;

                throw new DeviceInfoException();
            }
            catch (MeadowDeviceManagerException mdmEx)
            {
                throw new DeviceInfoException(mdmEx);
            }
        }

        public override async Task<bool> GetMonoRunStateAsync(CancellationToken cancellationToken =
                                                             default)
        {
            await _sendTargetData.SendSimpleCommand(
                                     HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_RUN_STATE,
                                     cancellationToken: cancellationToken)
                                 .ConfigureAwait(false);

            var tcs = new TaskCompletionSource<bool>();
            var result = false;

            EventHandler<MeadowMessageEventArgs> handler = (s, e) =>
            {
                if (e.MessageType == MeadowMessageType.Data)
                {
                    if (e.Message == "On reset, Meadow will start MONO and run app.exe")
                    {
                        result = true;
                        tcs.SetResult(true);
                    }
                    else if (e.Message
                          == "On reset, Meadow will not start MONO, therefore app.exe will not run")
                    {
                        result = false;
                        tcs.SetResult(true);
                    }
                }
            };

            DataProcessor.OnReceiveData += handler;

            await Task.WhenAny(tcs.Task, Task.Delay(5000, cancellationToken))
                      .ConfigureAwait(false);

            DataProcessor.OnReceiveData -= handler;

            return result;
        }

        public override async Task MonoDisableAsync(CancellationToken cancellationToken = default)
        {
            var endTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds(60));
            bool monoRunState;
            while ((monoRunState = await GetMonoRunStateAsync(cancellationToken)
                       .ConfigureAwait(false)) && endTime > DateTime.UtcNow)
            {
                await ProcessCommand(
                        HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_DISABLE,
                        MeadowMessageType.SerialReconnect,
                        timeoutMs: 15000,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                // Give the meadow a little time to cycle
                await Task.Delay(1000, cancellationToken)
                          .ConfigureAwait(false);

                await Initialize(cancellationToken)
                    .ConfigureAwait(false);

                await WaitForReadyAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            if (monoRunState)
                throw new Exception("Failed to stop mono.");
        }

        public override async Task MonoEnableAsync(CancellationToken cancellationToken = default)
        {
            await ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_ENABLE,
                MeadowMessageType.SerialReconnect,
                timeoutMs: 15000,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Give the meadow a little time to cycle
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);

            await Initialize(cancellationToken)
                .ConfigureAwait(false);

            await WaitForReadyAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        public override Task MonoFlashAsync(CancellationToken cancellationToken = default)
        {
            return ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_FLASH,
                timeoutMs: 200000,
                filter: e => e.Message.StartsWith("Mono runtime successfully flashed."),
                cancellationToken: cancellationToken);
        }

        public override async Task ResetMeadowAsync(CancellationToken cancellationToken = default)
        {
            await ProcessCommand(
                    HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESET_PRIMARY_MCU,
                    null,
                    doAcceptedCheck: false,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            // Give the meadow a little time to cycle
            await Task.Delay(1000,cancellationToken).ConfigureAwait(false);

            await Initialize(cancellationToken)
                .ConfigureAwait(false);

            await WaitForReadyAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        public override Task EnterDfuModeAsync(CancellationToken cancellationToken = default)
        {
            return ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENTER_DFU_MODE,
                null,
                doAcceptedCheck: false,
                cancellationToken: cancellationToken);
        }

        public override Task NshEnableAsync(CancellationToken cancellationToken = default)
        {
            return ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH,
                userData: (uint) 1,
                cancellationToken: cancellationToken);
        }

        // TODO: Is sending a 0 to HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH correct?
        public override Task NshDisableAsync(CancellationToken cancellationToken = default)
        {
            return ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH,
                userData: (uint) 0,
                cancellationToken: cancellationToken);
        }

        public override Task TraceEnableAsync(CancellationToken cancellationToken = default)
        {
            return ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_SEND_TRACE_TO_HOST,
                cancellationToken: cancellationToken);
        }

        public override Task TraceDisableAsync(CancellationToken cancellationToken = default)
        {
            return ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_NO_TRACE_TO_HOST,
                cancellationToken: cancellationToken);
        }

        public override Task QspiWriteAsync(int value, CancellationToken cancellationToken = default)
        {
            return ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_WRITE,
                userData: (uint) value,
                cancellationToken: cancellationToken);
        }

        public override Task QspiReadAsync(int value, CancellationToken cancellationToken = default)
        {
            return ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_READ,
                userData: (uint) value,
                cancellationToken: cancellationToken);
        }

        public override Task QspiInitAsync(int value, CancellationToken cancellationToken = default)
        {
            return ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_INIT,
                userData: (uint) value,
                cancellationToken: cancellationToken);
        }

        private protected async Task<string?> ProcessCommand(HcomMeadowRequestType requestType,
                                                             MeadowMessageType responseMessageType =
                                                                 MeadowMessageType.Concluded,
                                                             uint userData = 0,
                                                             bool doAcceptedCheck = true,
                                                             int timeoutMs = 10000,
                                                             CancellationToken cancellationToken =
                                                                 default,
                                                             [CallerMemberName] string? caller =
                                                                 null)
        {
            Logger.LogTrace($"{caller} sent {requestType} waiting for {responseMessageType}");
            var message = await ProcessCommand(
                                  requestType,
                                  e => e.MessageType == responseMessageType,
                                  userData,
                                  doAcceptedCheck,
                                  timeoutMs,
                                  cancellationToken)
                              .ConfigureAwait(false);

            Logger.LogTrace($"Returning to {caller} with {message ?? "[empty]"}");
            return message;
        }

        private protected async Task<string?> ProcessCommand(HcomMeadowRequestType requestType,
                                                             Predicate<MeadowMessageEventArgs>?
                                                                 filter,
                                                             uint userData = 0,
                                                             bool doAcceptedCheck = true,
                                                             int timeoutMs = 10000,
                                                             CancellationToken cancellationToken =
                                                                 default,
                                                             [CallerMemberName] string? caller =
                                                                 null)
        {
            Logger.LogTrace($"{caller} sent {requestType}");
            await _sendTargetData.SendSimpleCommand(
                                     requestType,
                                     userData,
                                     doAcceptedCheck,
                                     cancellationToken)
                                 .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            var (isSuccess, message, _) = await WaitForResponseMessage(filter, timeoutMs, cancellationToken)
                              .ConfigureAwait(false);

            Logger.LogTrace($"Returning to {caller} with {message ?? "[empty]"}");
            return message;
        }

        protected internal async Task<(bool Success, string? Message, MeadowMessageType MessageType)> WaitForResponseMessage(
            Predicate<MeadowMessageEventArgs>? filter,
            int millisecondDelay = 10000,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string? caller = null)
        {
            Logger.LogTrace($"{caller} waiting for response.");
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
                Logger.LogTrace($"Received {e.MessageType}, matches filter? {filter(e)}");
                if (filter(e))
                {
                    message = e.Message;
                    messageType = e.MessageType;
                    result = true;
                    tcs.SetResult(true);
                }
            };

            DataProcessor.OnReceiveData += handler;

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
                DataProcessor.OnReceiveData -= handler;
            }

            if (result)
            {
                Logger.LogTrace($"Returning to {caller} with {message ?? "[empty]"}");
                return (result, message, messageType);
            }


            throw new MeadowCommandException(message);
        }
    }
}