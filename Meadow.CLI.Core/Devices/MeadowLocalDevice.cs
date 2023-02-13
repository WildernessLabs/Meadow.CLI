using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Internals.MeadowCommunication;
using Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses;
using Meadow.Hcom;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.Devices
{
    public abstract partial class MeadowLocalDevice : IMeadowDevice
    {
        private protected TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

        public ILogger? Logger { get; }
        public MeadowDataProcessor DataProcessor { get; }
        public MeadowDeviceInfo? DeviceInfo { get; protected set; }
        public DebuggingServer DebuggingServer { get; }
        public IList<FileData> FilesOnDevice { get; } = new List<FileData>();

        protected MeadowLocalDevice(MeadowDataProcessor dataProcessor, ILogger? logger = null)
        {
            Logger = logger;
            DataProcessor = dataProcessor;
        }

        public abstract Task Write(byte[] encodedBytes,
                                        int encodedToSend,
                                        CancellationToken cancellationToken = default);

        public async Task<MeadowDeviceInfo?> GetDeviceInfo(TimeSpan timeout, CancellationToken cancellationToken = default)
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
                var commandResponse = await SendCommand(command, cancellationToken);

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
        public async Task<string?> GetDeviceName(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var info = await GetDeviceInfo(timeout, cancellationToken);

            return info?.Product ?? String.Empty;
        }

        public async Task<bool> GetMonoRunState(CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("Sending Mono Run State Request");

            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_RUN_STATE)
                    .WithResponseType(MeadowMessageType.Data)
                    .Build();

            var commandResponse =
                await SendCommand(command, cancellationToken);

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

        public async Task MonoDisable(CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("Sending Mono Disable Request");
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_DISABLE)
                    .WithCompletionResponseType(MeadowMessageType.SerialReconnect)
                    .WithResponseType(MeadowMessageType.SerialReconnect)
                    .Build();

            await SendCommand(command, cancellationToken);
        }

        public async Task MonoEnable(CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("Sending Mono Enable Request");
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_ENABLE)
                    .WithCompletionResponseType(MeadowMessageType.SerialReconnect)
                    .WithResponseType(MeadowMessageType.SerialReconnect)
                    .Build();

            await SendCommand(command, cancellationToken);
        }

        public Task MonoFlash(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_FLASH)
                    .WithCompletionFilter(
                        e => e.Message.StartsWith("Mono runtime successfully flashed."))
                    .WithTimeout(TimeSpan.FromMinutes(5))
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public async Task ResetMeadow(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESET_PRIMARY_MCU)
                    .WithCompletionResponseType(MeadowMessageType.SerialReconnect)
                    .WithResponseType(MeadowMessageType.SerialReconnect)
                    .Build();

            await SendCommand(command, cancellationToken);
        }

        public Task EnterDfuMode(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENTER_DFU_MODE)
                    .WithCompletionResponseType(MeadowMessageType.Accepted)
                    .WithResponseFilter(x => true)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task NshEnable(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH)
                    .WithUserData(1)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task NshDisable(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH)
                    .WithUserData(0)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task TraceEnable(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_SEND_TRACE_TO_HOST)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task Uart1Trace(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_SEND_TRACE_TO_UART)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task TraceDisable(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_NO_TRACE_TO_HOST)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task SetTraceLevel(uint traceLevel, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_CHANGE_TRACE_LEVEL)
                    .WithUserData(traceLevel)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task SetDeveloper1(uint userData, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_1)
                    .WithUserData(userData)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task SetDeveloper2(uint userData, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_2)
                    .WithUserData(userData)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task SetDeveloper3(uint userData, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_3)
                    .WithUserData(userData)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task SetDeveloper4(uint userData, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_4)
                    .WithUserData(userData)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task Uart1Apps(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_NO_TRACE_TO_UART)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task QspiWrite(int value, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_WRITE)
                    .WithUserData((uint)value)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task QspiRead(int value, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_READ)
                    .WithUserData((uint)value)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task QspiInit(int value, CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_S25FL_QSPI_INIT)
                    .WithUserData((uint)value)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public async Task StartDebugging(int port, CancellationToken cancellationToken)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_START_DBG_SESSION)
                    .WithCompletionResponseType(MeadowMessageType.Accepted)
                    .WithResponseType(MeadowMessageType.Accepted)
                    .Build();

            await SendCommand(command, cancellationToken);
        }

        public Task RestartEsp32(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESTART_ESP32)
                    .WithCompletionResponseType(MeadowMessageType.Concluded)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public async Task<string?> GetDeviceMacAddress(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(
                    HcomMeadowRequestType.HCOM_MDOW_REQUEST_READ_ESP_MAC_ADDRESS).Build();

            var commandResponse =
                await SendCommand(command, cancellationToken);

            return commandResponse.Message;
        }

        public async Task<DateTimeOffset> GetRtcTime(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(
                    HcomMeadowRequestType.HCOM_MDOW_REQUEST_RTC_READ_TIME_CMD)
                .WithResponseType(MeadowMessageType.Data)
                .Build();

            var commandResponse =
                await SendCommand(command, cancellationToken);

            // return will be in the format "UTC time:2022-10-22T10:40:19+0:00"
            return DateTimeOffset.Parse(commandResponse.Message.Substring(9));
        }

        public async Task SetRtcTime(DateTimeOffset dateTime, CancellationToken cancellationToken)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_RTC_SET_TIME_CMD
                )
                    .WithCompletionResponseType(MeadowMessageType.Accepted)
                    .WithResponseType(MeadowMessageType.Accepted)
                    .WithData(Encoding.ASCII.GetBytes(dateTime.ToString("o")))
                    .Build();

            await SendCommand(command, cancellationToken);
        }

        public async Task<string> CloudRegisterDevice(CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Sending Meadow Cloud registration request");

            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_OTA_REGISTER_DEVICE)
                    .WithResponseType(MeadowMessageType.DevicePublicKey)
                    .WithCompletionResponseType(MeadowMessageType.Concluded)
                    .WithTimeout(new TimeSpan(hours: 0, minutes: 5, seconds: 0)) // RSA keypair generation on device takes a while
                    .Build();

            var commandResponse =
                await SendCommand(command, cancellationToken);

            return commandResponse.Message;
        }

        public abstract Task<bool> Initialize(CancellationToken cancellationToken);

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