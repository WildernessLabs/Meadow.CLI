using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.NewDeviceManagement.MeadowComms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meadow.CLI.Core.NewDeviceManagement
{
    public abstract partial class MeadowLocalDevice : MeadowDevice
    {
        private readonly SendTargetData _sendTargetData;
        protected readonly ILogger Logger;

        protected MeadowLocalDevice(MeadowDataProcessor dataProcessor, ILogger? logger = null)
            : base(dataProcessor)
        {
            _sendTargetData = new SendTargetData(this);
            Logger = logger ?? new NullLogger<MeadowSerialDevice>();
        }

        public abstract Task Write(byte[] encodedBytes, int encodedToSend);

        public abstract Task<bool> Initialize(CancellationToken cancellationToken = default);

        //device Id information is processed when the message is received
        //this will request the device Id and return true it was set successfully
        public override async Task<string> GetDeviceInfo(int timeoutMs = 5000,
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
                return await WaitForResponseMessage(
                               p => p.MessageType == MeadowMessageType.DeviceInfo,
                               millisecondDelay: timeoutMs,
                               cancellationToken: cancellationToken)
                           .ConfigureAwait(false);
            }
            catch (MeadowDeviceManagerException mdmEx)
            {
                throw new DeviceInfoException(mdmEx);
            }
        }

        //device name is processed when the message is received
        //this will request the device name and return true it was successfully
        public override async Task<string> GetDeviceName(int timeoutMs = 500,
                                                         CancellationToken cancellationToken =
                                                             default)
        {
            await _sendTargetData.SendSimpleCommand(
                                     HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_DEVICE_NAME,
                                     cancellationToken: cancellationToken)
                                 .ConfigureAwait(false);

            try
            {
                return await WaitForResponseMessage(
                               p => p.MessageType == MeadowMessageType.DeviceInfo,
                               millisecondDelay: timeoutMs,
                               cancellationToken: cancellationToken)
                           .ConfigureAwait(false);
            }
            catch (MeadowDeviceManagerException mdmEx)
            {
                throw new DeviceInfoException(mdmEx);
            }
        }

        public override async Task<bool> GetMonoRunState(CancellationToken cancellationToken = default)
        {
            await _sendTargetData.SendSimpleCommand(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_RUN_STATE, cancellationToken: cancellationToken).ConfigureAwait(false);

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
                    else if (e.Message == "On reset, Meadow will not start MONO, therefore app.exe will not run")
                    {
                        result = false;
                        tcs.SetResult(true);
                    }
                }
            };

            DataProcessor.OnReceiveData += handler;

            await Task.WhenAny(tcs.Task, Task.Delay(5000, cancellationToken)).ConfigureAwait(false);

            DataProcessor.OnReceiveData -= handler;

            return result;
        }

        public override Task MonoDisable(CancellationToken cancellationToken = default)
        {
            return ProcessCommand(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_DISABLE, MeadowMessageType.SerialReconnect, timeoutMs: 15000, cancellationToken: cancellationToken);
        }

        public override Task MonoEnable(CancellationToken cancellationToken = default)
        {
            return ProcessCommand(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_ENABLE, MeadowMessageType.SerialReconnect, timeoutMs: 15000, cancellationToken: cancellationToken);
        }

        public override async Task ResetMeadow(CancellationToken cancellationToken = default)
        {
            await ProcessCommand(HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESET_PRIMARY_MCU, null, doAcceptedCheck:false, cancellationToken: cancellationToken).ConfigureAwait(false);
            await Initialize(cancellationToken)
                .ConfigureAwait(false);
        }

        public override Task EnterDfuMode(CancellationToken cancellationToken = default)
        {
            return ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENTER_DFU_MODE,
                null,
                doAcceptedCheck: false,
                cancellationToken: cancellationToken);
        }

        private protected async Task<string?> ProcessCommand(HcomMeadowRequestType requestType,
                                                             MeadowMessageType responseMessageType =
                                                                 MeadowMessageType.Concluded,
                                                             uint userData = 0,
                                                             bool doAcceptedCheck = true,
                                                             int timeoutMs = 10000,
                                                             CancellationToken cancellationToken = default, [CallerMemberName]string? caller = null)
        {
            Logger.LogTrace($"{caller} sent {requestType} waiting for {responseMessageType}");
            var message = await ProcessCommand(
                requestType,
                e => e.MessageType == responseMessageType,
                userData,
                doAcceptedCheck,
                timeoutMs,
                cancellationToken).ConfigureAwait(false);
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
                                                                 default, [CallerMemberName]string? caller = null)
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
            var message = await WaitForResponseMessage(filter, timeoutMs, cancellationToken)
                           .ConfigureAwait(false);
            Logger.LogTrace($"Returning to {caller} with {message ?? "[empty]"}");
            return message;
        }

        private protected async Task<string> WaitForResponseMessage(
            Predicate<MeadowMessageEventArgs>? filter,
            int millisecondDelay = 10000,
            CancellationToken cancellationToken = default, [CallerMemberName]string? caller = null)
        {
            Logger.LogTrace($"{caller} waiting for response.");
            if (filter == null)
            {
                return string.Empty;
            }

            var tcs = new TaskCompletionSource<bool>();
            var result = false;
            var message = string.Empty;

            EventHandler<MeadowMessageEventArgs> handler = (s, e) =>
            {
                Logger.LogTrace($"Received {e.MessageType}, matches filter? {filter(e)}");
                if (filter(e))
                {
                    message = e?.Message;
                    result = true;
                    tcs.SetResult(true);
                }
            };

            DataProcessor.OnReceiveData += handler;

            await Task.WhenAny(tcs.Task, Task.Delay(millisecondDelay, cancellationToken))
                      .ConfigureAwait(false);

            DataProcessor.OnReceiveData -= handler;

            if (result)
            {
                Logger.LogTrace($"Returning to {caller} with {message ?? "[empty]"}");
                return message;
            }

            
            throw new MeadowCommandException(message);
        }
    }
}