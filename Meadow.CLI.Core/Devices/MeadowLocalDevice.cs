using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Internals.MeadowCommunication;
using Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.Devices
{
    public abstract partial class MeadowLocalDevice : IMeadowDevice
    {
        private protected TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

        public ILogger Logger { get; }
        public MeadowDataProcessor DataProcessor { get; }
        public MeadowDeviceInfo? DeviceInfo { get; protected set; }
        public DebuggingServer DebuggingServer { get; }
        public IDictionary<string, uint> FilesOnDevice { get; } = new SortedDictionary<string, uint>();

        protected MeadowLocalDevice(MeadowDataProcessor dataProcessor, ILogger? logger = null)
        {
            Logger = logger;
            DataProcessor = dataProcessor;
        }

        public abstract Task WriteAsync(byte[] encodedBytes,
                                        int encodedToSend,
                                        CancellationToken cancellationToken = default);

        public async Task<MeadowDeviceInfo?> GetDeviceInfoAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            DeviceInfo = null;

            var command = new SimpleCommandBuilder(
                              HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_DEVICE_INFORMATION)
                          .WithTimeout(timeout)
                          .WithResponseType(MeadowMessageType.DeviceInfo)
                          .WithCompletionResponseType(MeadowMessageType.Concluded)
                          .Build();


            try
            {
                var retryCount = 1;

            Retry:
                var commandResponse = await SendCommandAsync(command, cancellationToken);

                if (commandResponse.IsSuccess)
                {
                    if (commandResponse.Message == String.Empty)
                    { // TODO: this feels like a bug lower down or in HCOM, but I can reproduce it regularly (3 Oct 2022)
                        if (--retryCount >= 0)
                        {
                            goto Retry;
                        }
                    }

                    DeviceInfo = new MeadowDeviceInfo(commandResponse.Message!);
                    return DeviceInfo;
                }

                throw new DeviceInfoException();
            }
            catch (MeadowDeviceManagerException mdmEx)
            {
                throw new DeviceInfoException(mdmEx);
            }
        }

        //device name is processed when the message is received
        //this will request the device name and return true it was successfully
        public async Task<string?> GetDeviceNameAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var info = await GetDeviceInfoAsync(timeout, cancellationToken);

            return info?.Product ?? String.Empty;
        }

        public async Task<bool> GetMonoRunStateAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("Sending Mono Run State Request");

            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_RUN_STATE)
                    .WithResponseType(MeadowMessageType.Data)
                    .Build();

            var commandResponse =
                await SendCommandAsync(command, cancellationToken);

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

        public async Task MonoDisableAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("Sending Mono Disable Request");
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_DISABLE)
                    .WithCompletionResponseType(MeadowMessageType.SerialReconnect)
                    .WithResponseType(MeadowMessageType.SerialReconnect)
                    .Build();

            await SendCommandAsync(command, cancellationToken);
        }

        public async Task MonoEnableAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("Sending Mono Enable Request");
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_ENABLE)
                    .WithCompletionResponseType(MeadowMessageType.SerialReconnect)
                    .WithResponseType(MeadowMessageType.SerialReconnect)
                    .Build();

            await SendCommandAsync(command, cancellationToken);
        }

        public Task MonoFlashAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_FLASH)
                    .WithCompletionFilter(
                        e => e.Message.StartsWith("Mono runtime successfully flashed."))
                    .WithTimeout(TimeSpan.FromMinutes(5))
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public async Task ResetMeadowAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESET_PRIMARY_MCU)
                    .WithCompletionResponseType(MeadowMessageType.SerialReconnect)
                    .WithResponseType(MeadowMessageType.SerialReconnect)
                    .Build();

            await SendCommandAsync(command, cancellationToken);
        }

        public Task EnterDfuModeAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENTER_DFU_MODE)
                    .WithCompletionResponseType(MeadowMessageType.Accepted)
                    .WithResponseFilter(x => true)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public Task NshEnableAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH)
                    .WithUserData(1)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public Task NshDisableAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH)
                    .WithUserData(0)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public Task TraceEnableAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_SEND_TRACE_TO_HOST)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public Task Uart1Trace(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_SEND_TRACE_TO_UART)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public Task TraceDisableAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_NO_TRACE_TO_HOST)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public Task SetTraceLevelAsync(uint traceLevel, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_CHANGE_TRACE_LEVEL)
                    .WithUserData(traceLevel)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public Task SetDeveloper1(uint userData, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_1)
                    .WithUserData(userData)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public Task SetDeveloper2(uint userData, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_2)
                    .WithUserData(userData)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public Task SetDeveloper3(uint userData, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_3)
                    .WithUserData(userData)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public Task SetDeveloper4(uint userData, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_4)
                    .WithUserData(userData)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public Task Uart1Apps(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_NO_TRACE_TO_UART)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public Task QspiWriteAsync(int value, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_WRITE)
                    .WithUserData((uint)value)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public Task QspiReadAsync(int value, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_READ)
                    .WithUserData((uint)value)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public Task QspiInitAsync(int value, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_INIT)
                    .WithUserData((uint)value)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public async Task StartDebuggingAsync(int port, CancellationToken cancellationToken)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_START_DBG_SESSION)
                    .WithCompletionResponseType(MeadowMessageType.Accepted)
                    .WithResponseType(MeadowMessageType.Accepted)
                    .Build();

            await SendCommandAsync(command, cancellationToken);
        }

        public Task RestartEsp32Async(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESTART_ESP32)
                    .WithCompletionResponseType(MeadowMessageType.Concluded)
                    .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public async Task<string?> GetDeviceMacAddressAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(
                    HcomMeadowRequestType.HCOM_MDOW_REQUEST_READ_ESP_MAC_ADDRESS).Build();

            var commandResponse =
                await SendCommandAsync(command, cancellationToken);

            return commandResponse.Message;
        }

        public abstract Task<bool> InitializeAsync(CancellationToken cancellationToken);

        public abstract bool IsDeviceInitialized();

        private protected abstract void Dispose(bool disposing);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MeadowLocalDevice()
        {
            Dispose(false);
        }
    }
}