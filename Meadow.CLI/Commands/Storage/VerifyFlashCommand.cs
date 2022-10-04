using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Storage
{
    [Command("flash verify", Description = "Verify the contents of the flash were deleted")]
    public class VerifyFlashCommand : MeadowSerialCommand
    {
        private readonly ILogger<VerifyFlashCommand> _logger;

        public VerifyFlashCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<VerifyFlashCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            _logger.LogInformation("Verifying flash");
            await Meadow.VerifyErasedFlashAsync(cancellationToken);
        }
    }
}
