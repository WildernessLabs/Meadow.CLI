using System;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement
{
    [Command("device mac", Description = "Get the device info")]
    public class GetDeviceMacAddressCommand : MeadowSerialCommand
    {
        private readonly ILogger<GetDeviceInfoCommand> _logger;

        public GetDeviceMacAddressCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<GetDeviceInfoCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device =
                await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, cancellationToken).ConfigureAwait(false);

            var macAddress = await device.GetDeviceMacAddressAsync(cancellationToken).ConfigureAwait(false);
            if (macAddress == null)
            {
                _logger.LogInformation("Unable to retrieve device mac address");
            }
            else
            {
                _logger.LogInformation("Device MAC: {macAddress}", macAddress);
            }
        }
    }
}
