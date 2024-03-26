using System;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement
{
    [Command("device name", Description = "Get the name of the Meadow")]
    public class GetDeviceNameCommand : MeadowSerialCommand
    {
        private readonly ILogger<GetDeviceNameCommand> _logger;

        public GetDeviceNameCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<GetDeviceNameCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            var deviceName = await Meadow.GetDeviceName(TimeSpan.FromSeconds(60), cancellationToken: cancellationToken);

            _logger.LogInformation($"Device Name: {deviceName}");
        }
    }
}