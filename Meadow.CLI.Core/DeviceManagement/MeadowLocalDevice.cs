using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Meadow.CLI.Core.Internals.MeadowCommunication;
using Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Core.DeviceManagement
{
    public abstract partial class MeadowLocalDevice : MeadowDevice
    {
        protected MeadowLocalDevice(MeadowDataProcessor dataProcessor, ILogger? logger = null)
            : base(dataProcessor, logger)
        {
        }

        public abstract Task WriteAsync(byte[] encodedBytes,
                                        int encodedToSend,
                                        CancellationToken cancellationToken = default);

        public abstract Task<bool> Initialize(CancellationToken cancellationToken = default);

        //device Id information is processed when the message is received
        //this will request the device Id and return true it was set successfully
        public override async Task<MeadowDeviceInfo> GetDeviceInfoAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            await Initialize(cancellationToken).ConfigureAwait(false);

            var command = new SimpleCommandBuilder(
                              HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_DEVICE_INFORMATION)
                          .WithTimeout(timeout)
                          .WithResponseType(MeadowMessageType.DeviceInfo)
                          .WithCompletionResponseType(MeadowMessageType.DeviceInfo)
                          .Build();


            try
            {
                var commandResponse =
                    await SendCommandAsync(command, cancellationToken)
                        .ConfigureAwait(false);

                if (commandResponse.IsSuccess)
                    return new MeadowDeviceInfo(commandResponse.Message!);

                throw new DeviceInfoException();
            }
            catch (MeadowDeviceManagerException mdmEx)
            {
                throw new DeviceInfoException(mdmEx);
            }
        }

        //device name is processed when the message is received
        //this will request the device name and return true it was successfully
        public override async Task<string?> GetDeviceNameAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var info = await GetDeviceInfoAsync(timeout, cancellationToken)
                           .ConfigureAwait(false);

            return info.Name;
        }

        public override async Task<bool> GetMonoRunStateAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("Sending Mono Run State Request");

            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_RUN_STATE)
                    .WithResponseType(MeadowMessageType.Data)
                    .Build();

            var commandResponse =
                await SendCommandAsync(command, cancellationToken)
                    .ConfigureAwait(false);

            var result = false;
            switch (commandResponse.Message)
            {
                case "On reset, Meadow will start MONO and run app.exe":
                case "Mono is enabled":
                    result = true;
                    break;
                case "On reset, Meadow will not start MONO, therefore app.exe will not run":
                case "Mono is disabled":
                    result = false;
                    break;
            }

            Logger.LogDebug("Mono Run State: {runState}", result ? "enabled" : "disabled");
            return result;
        }

        public override async Task MonoDisableAsync(CancellationToken cancellationToken = default)
        {
            var endTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds(60));
            bool monoRunState;
            while ((monoRunState = await GetMonoRunStateAsync(cancellationToken)
                                       .ConfigureAwait(false))
                && endTime > DateTime.UtcNow)
            {
                Logger.LogDebug("Sending Mono Disable Request");
                var command =
                    new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_DISABLE)
                        .WithCompletionResponseType(MeadowMessageType.SerialReconnect)
                        .WithResponseType(MeadowMessageType.SerialReconnect)
                        .Build();

                await SendCommandAsync(command, cancellationToken)
                    .ConfigureAwait(false);

                Logger.LogDebug("Waiting for Meadow to cycle");
                await Task.Delay(1000, cancellationToken)
                          .ConfigureAwait(false);

                Logger.LogDebug("Re-initialize the device");
                await Initialize(cancellationToken).ConfigureAwait(false);

                Logger.LogDebug("Waiting for the Meadow to be ready");
                await WaitForReadyAsync(DefaultTimeout, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            if (monoRunState)
                throw new Exception("Failed to stop mono.");
        }

        public override async Task MonoEnableAsync(CancellationToken cancellationToken = default)
        {
            var endTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds(60));
            bool monoRunState;
            while ((monoRunState = await GetMonoRunStateAsync(cancellationToken).ConfigureAwait(false)) == false
                && endTime > DateTime.UtcNow)
            {
                Logger.LogDebug("Sending Mono Enable Request");
                var command =
                    new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_ENABLE)
                        .WithCompletionResponseType(MeadowMessageType.SerialReconnect)
                        .WithResponseType(MeadowMessageType.SerialReconnect)
                        .Build();

                await SendCommandAsync(command, cancellationToken)
                    .ConfigureAwait(false);

                Logger.LogDebug("Waiting for Meadow to cycle");
                await Task.Delay(500, cancellationToken)
                          .ConfigureAwait(false);

                Logger.LogDebug("Re-initialize the device");
                await Initialize(cancellationToken).ConfigureAwait(false);

                Logger.LogDebug("Waiting for the Meadow to be ready");
                await WaitForReadyAsync(DefaultTimeout, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!monoRunState)
                throw new Exception("Failed to enable mono.");
        }

        public override Task MonoFlashAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_FLASH)
                    .WithCompletionFilter(
                        e => e.Message.StartsWith("Mono runtime successfully flashed."))
                    .WithTimeout(TimeSpan.FromMinutes(5))
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public override async Task ResetMeadowAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESET_PRIMARY_MCU)
                    .WithCompletionResponseType(MeadowMessageType.SerialReconnect)
                    .WithResponseType(MeadowMessageType.SerialReconnect)
                    .Build();

            await SendCommandAsync(command, cancellationToken)
                .ConfigureAwait(false);

            // Give the meadow a little time to cycle
            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            await Initialize(cancellationToken).ConfigureAwait(false);

            await WaitForReadyAsync(DefaultTimeout, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        public override Task EnterDfuModeAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENTER_DFU_MODE)
                    .WithCompletionResponseType(MeadowMessageType.Accepted)
                    .WithResponseFilter(x => true)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public override Task NshEnableAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH)
                    .WithUserData(1)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        // TODO: Is sending a 0 to HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH correct?
        public override Task NshDisableAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH)
                    .WithUserData(0)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public override Task TraceEnableAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_SEND_TRACE_TO_HOST)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public override Task TraceDisableAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_NO_TRACE_TO_HOST)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public override Task SetTraceLevelAsync(uint traceLevel,
                                                CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_CHANGE_TRACE_LEVEL)
                    .WithUserData(traceLevel)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public override Task QspiWriteAsync(int value,
                                            CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_WRITE)
                    .WithUserData((uint)value)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public override Task QspiReadAsync(int value, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_READ)
                    .WithUserData((uint)value)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public override Task QspiInitAsync(int value, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_INIT)
                    .WithUserData((uint)value)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public override async Task StartDebuggingSessionAsync(int port, CancellationToken cancellationToken)
        {
            DataProcessor.ForwardDebuggingData = ForwardMonoDataToVisualStudioAsync;
            var endpoint = new IPEndPoint(IPAddress.Loopback, port);
            DebuggingServer = new DebuggingServer(this, endpoint, Logger);

            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_START_DBG_SESSION)
                    .WithCompletionResponseType(MeadowMessageType.Accepted)
                    .WithResponseType(MeadowMessageType.Accepted)
                    .Build();

            await SendCommandAsync(command, cancellationToken);

            await Initialize(cancellationToken).ConfigureAwait(false);

            await DebuggingServer.StartListeningAsync().ConfigureAwait(false);
        }

        public override Task RestartEsp32Async(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESTART_ESP32)
                    .WithCompletionResponseType(MeadowMessageType.Concluded)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public override async Task<string?> GetDeviceMacAddressAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(
                    HcomMeadowRequestType.HCOM_MDOW_REQUEST_READ_ESP_MAC_ADDRESS).Build();

            var commandResponse =
                await SendCommandAsync(command, cancellationToken)
                    .ConfigureAwait(false);

            return commandResponse.Message;
        }
    }
}