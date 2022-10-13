using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Mono
{
    [Command("mono state", Description = "Returns whether or not mono is enabled or disabled on the Meadow device")]
    public class MonoRunStateCommand : MeadowSerialCommand
    {
        private readonly ILogger<MonoRunStateCommand> _logger;

        public MonoRunStateCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory)
            : base(downloadManager, loggerFactory)
        {
            _logger = LoggerFactory.CreateLogger<MonoRunStateCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            var runState = await Meadow.GetMonoRunState(cancellationToken);

            _logger.LogInformation($"Mono Run State: {(runState ? "Enabled" : "Disabled")}");
        }
    }
}