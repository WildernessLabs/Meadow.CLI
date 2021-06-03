using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Utility
{
    [Command("download os", Description = "Download the latest Meadow OS")]
    public class DownloadOsCommand : MeadowCommand
    {
        private readonly ILogger<InstallDfuUtilCommand> _logger;
        public DownloadOsCommand(ILoggerFactory loggerFactory) : base(loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<InstallDfuUtilCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            var downloadManager = new DownloadManager(_logger);
            await downloadManager.DownloadLatestAsync().ConfigureAwait(false);
        }
    }
}
