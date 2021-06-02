using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement
{
    [Command("device name", Description = "Get the name of the Meadow")]
    public class GetDeviceNameCommand : MeadowSerialCommand
    {
        private readonly ILogger<GetDeviceNameCommand> _logger;

        public GetDeviceNameCommand(ILoggerFactory loggerFactory,
                                    MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<GetDeviceNameCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();
            using var device = await MeadowDeviceManager
                                     .GetMeadowForSerialPort(
                                         SerialPortName,
                                         cancellationToken)
                                     .ConfigureAwait(false);

            var deviceName = await device.GetDeviceName(cancellationToken: cancellationToken)
                                         .ConfigureAwait(false);

            _logger.LogInformation($"Device Name: {deviceName}");
        }
    }
}