using System.Threading.Tasks;

using CliFx.Attributes;
using CliFx.Infrastructure;

using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;

using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Utility
{
    [Command("list ports", Description = "List available COM ports")]
    public class ListPortsCommand : MeadowCommand
    {
        private readonly ILogger<InstallDfuUtilCommand> _logger;

        public ListPortsCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory) : base(downloadManager, loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<InstallDfuUtilCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            foreach (var port in await MeadowDeviceManager.GetSerialPorts())
            {
                _logger.LogInformation("Found Meadow: {port}", port);
            }
        }
    }
}