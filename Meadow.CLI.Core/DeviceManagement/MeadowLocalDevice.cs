using System;
using System.Threading;
using System.Threading.Tasks;

using Meadow.CLI.Core.Internals.MeadowCommunication;

using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Core.DeviceManagement
{
    public abstract partial class MeadowLocalDevice : MeadowDevice
    {
        protected MeadowLocalDevice(MeadowDataProcessor dataProcessor, ILogger? logger = null)
            : base(dataProcessor, logger)
        {
        }

        public abstract Task WriteAsync(byte[] encodedBytes, int encodedToSend, CancellationToken cancellationToken = default);

        public abstract Task<bool> InitializeAsync(CancellationToken cancellationToken = default);

        //device Id information is processed when the message is received
        //this will request the device Id and return true it was set successfully
        public override async Task<string?> GetDeviceInfoAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken)
                .ConfigureAwait(false);

            await SendAcknowledgedSimpleCommand(
                                     HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_DEVICE_INFORMATION,
                                     cancellationToken: cancellationToken)
                                 .ConfigureAwait(false);

            try
            {
                var (isSuccess, message, messageType) = await WaitForSimpleResponseAsync(
                               MeadowMessageType.DeviceInfo,
                               timeout,
                               cancellationToken)
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
        public override async Task<string?> GetDeviceNameAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            await SendAcknowledgedSimpleCommand(
                                     HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_DEVICE_NAME,
                                     cancellationToken: cancellationToken)
                                 .ConfigureAwait(false);

            try
            {
                var (isSuccess, message, _) = await WaitForSimpleResponseAsync(
                                                                 MeadowMessageType.DeviceInfo,
                                                                 timeout,
                                                                 cancellationToken)
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

        public override async Task<bool> GetMonoRunStateAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("Sending Mono Run State Request");
            
            bool ResponseFilter(MeadowMessageEventArgs p)
            {
                if (p.MessageType != MeadowMessageType.Data) return false;

                Logger.LogTrace("Received Message: {message}", p.Message);
                switch (p.Message)
                {
                    case "On reset, Meadow will start MONO and run app.exe":
                    case "Mono is enabled":
                    case "On reset, Meadow will not start MONO, therefore app.exe will not run":
                    case "Mono is disabled":
                        return true;
                    default:
                        return false;
                }
            }

            await SendAcknowledgedSimpleCommand(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_RUN_STATE,
                                                cancellationToken: cancellationToken)
                                 .ConfigureAwait(false);

            var (isSuccess, message, _) = await WaitForSimpleResponseAsync(ResponseFilter, cancellationToken: cancellationToken);
            
            var result = false;
            switch (message)
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
            while ((monoRunState = await GetMonoRunStateAsync(cancellationToken).ConfigureAwait(false)) && endTime > DateTime.UtcNow)
            {
                Logger.LogDebug("Sending Mono Disable Request");
                await SendCommandAndWaitForResponseAsync(
                        HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_DISABLE,
                        MeadowMessageType.SerialReconnect,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                Logger.LogDebug("Waiting for Meadow to cycle");
                await Task.Delay(1000, cancellationToken)
                          .ConfigureAwait(false);

                Logger.LogDebug("Re-initialize the device");
                await InitializeAsync(cancellationToken)
                    .ConfigureAwait(false);

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
            while ((monoRunState = await GetMonoRunStateAsync(cancellationToken).ConfigureAwait(false)) == false && endTime > DateTime.UtcNow)
            {
                Logger.LogDebug("Sending Mono Enable Request");
                await SendCommandAndWaitForResponseAsync(
                        HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_ENABLE,
                        MeadowMessageType.SerialReconnect,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                Logger.LogDebug("Waiting for Meadow to cycle");
                await Task.Delay(500, cancellationToken)
                          .ConfigureAwait(false);

                Logger.LogDebug("Re-initialize the device");
                await InitializeAsync(cancellationToken)
                    .ConfigureAwait(false);

                Logger.LogDebug("Waiting for the Meadow to be ready");
                await WaitForReadyAsync(DefaultTimeout, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!monoRunState)
                throw new Exception("Failed to enable mono.");
        }

        public override Task MonoFlashAsync(CancellationToken cancellationToken = default)
        {
            return SendCommandAndWaitForResponseAsync(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_FLASH,
                filter: e => e.Message.StartsWith("Mono runtime successfully flashed."),
                timeout: TimeSpan.FromMinutes(5),
                cancellationToken: cancellationToken);
        }

        public override async Task ResetMeadowAsync(CancellationToken cancellationToken = default)
        {
            await SendCommandAndWaitForResponseAsync(
                    HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESET_PRIMARY_MCU,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            // Give the meadow a little time to cycle
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);

            await InitializeAsync(cancellationToken)
                .ConfigureAwait(false);

            await WaitForReadyAsync(DefaultTimeout, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        public override Task EnterDfuModeAsync(CancellationToken cancellationToken = default)
        {
            return SendCommandAndWaitForResponseAsync(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENTER_DFU_MODE,
                null,
                cancellationToken: cancellationToken);
        }

        public override Task NshEnableAsync(CancellationToken cancellationToken = default)
        {
            return SendCommandAndWaitForResponseAsync(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH,
                userData: (uint)1,
                cancellationToken: cancellationToken);
        }

        // TODO: Is sending a 0 to HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH correct?
        public override Task NshDisableAsync(CancellationToken cancellationToken = default)
        {
            return SendCommandAndWaitForResponseAsync(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH,
                userData: (uint)0,
                cancellationToken: cancellationToken);
        }

        public override Task TraceEnableAsync(CancellationToken cancellationToken = default)
        {
            return SendCommandAndWaitForResponseAsync(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_SEND_TRACE_TO_HOST,
                cancellationToken: cancellationToken);
        }

        public override Task TraceDisableAsync(CancellationToken cancellationToken = default)
        {
            return SendCommandAndWaitForResponseAsync(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_NO_TRACE_TO_HOST,
                cancellationToken: cancellationToken);
        }

        public override Task QspiWriteAsync(int value, CancellationToken cancellationToken = default)
        {
            return SendCommandAndWaitForResponseAsync(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_WRITE,
                userData: (uint)value,
                cancellationToken: cancellationToken);
        }

        public override Task QspiReadAsync(int value, CancellationToken cancellationToken = default)
        {
            return SendCommandAndWaitForResponseAsync(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_READ,
                userData: (uint)value,
                cancellationToken: cancellationToken);
        }

        public override Task QspiInitAsync(int value, CancellationToken cancellationToken = default)
        {
            return SendCommandAndWaitForResponseAsync(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_INIT,
                userData: (uint)value,
                cancellationToken: cancellationToken);
        }

        public override Task RestartEsp32Async(CancellationToken cancellationToken = default)
        {
            return SendCommandAndWaitForResponseAsync(HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESTART_ESP32, cancellationToken: cancellationToken);
        }

        public override Task<string?> GetDeviceMacAddressAsync(CancellationToken cancellationToken = default)
        {
            return SendCommandAndWaitForResponseAsync(HcomMeadowRequestType.HCOM_MDOW_REQUEST_READ_ESP_MAC_ADDRESS,
                                  cancellationToken: cancellationToken);
        }
    }
}
