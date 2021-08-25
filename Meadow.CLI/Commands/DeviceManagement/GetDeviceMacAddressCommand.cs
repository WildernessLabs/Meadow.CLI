using System;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement
{
    [Command("device mac", Description = "Read the ESP32's MAC address")]
    public class GetDeviceMacAddressCommand : MeadowSerialCommand
    {
        private readonly ILogger<GetDeviceInfoCommand> _logger;

        public GetDeviceMacAddressCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<GetDeviceInfoCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            var macAddress = await Meadow.GetDeviceMacAddressAsync(cancellationToken).ConfigureAwait(false);
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
