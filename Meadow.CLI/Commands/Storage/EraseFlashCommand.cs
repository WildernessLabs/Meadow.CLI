using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Storage
{
    [Command("flash erase", Description = "Erase the flash on the Meadow Board")]
    public class EraseFlashCommand : MeadowSerialPortCommand
    {
        private readonly ILogger<EraseFlashCommand> _logger;

        public EraseFlashCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory)
            : base(downloadManager, loggerFactory)
        {
            _logger = LoggerFactory.CreateLogger<EraseFlashCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            _logger.LogInformation("Erasing flash.");
            await Meadow.EraseFlash(cancellationToken);
        }
    }
}