using System;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement
{
    [Command("device info", Description = "Get the device info")]
    public class GetDeviceInfoCommand : MeadowSerialCommand
    {
        private readonly ILogger<GetDeviceInfoCommand> _logger;

        public GetDeviceInfoCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory)
            : base(downloadManager, loggerFactory)
        {
            _logger = LoggerFactory.CreateLogger<GetDeviceInfoCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);
            var cancellationToken = console.RegisterCancellationHandler();

            var deviceInfo = await Meadow.GetDeviceInfo(TimeSpan.FromSeconds(60), cancellationToken);
            _logger.LogInformation(deviceInfo.ToString());
        }
    }
}
