using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Mono
{
    [Command("mono flash", Description = "Erases the runtime flash and copies the runtime binary from the file partition to the runtime flash")]
    public class MonoFlashCommand : MeadowSerialPortCommand
    {
        private readonly ILogger<MonoRunStateCommand> _logger;

        public MonoFlashCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory)
            : base(downloadManager, loggerFactory)
        {
            _logger = LoggerFactory.CreateLogger<MonoRunStateCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.FlashMonoRuntime(cancellationToken);

            _logger.LogInformation($"Mono flashed successfully");
        }
    }
}