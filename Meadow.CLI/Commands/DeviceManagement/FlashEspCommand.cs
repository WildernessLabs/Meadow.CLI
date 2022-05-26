using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement
{
    [Command("flash esp", Description = "Flash the ESP co-processor")]
    public class FlashEspCommand : MeadowSerialCommand
    {
        [CommandOption("osVersion", 'v', Description = "Flash the ESP from a specific downloaded OS version - x.x.x.x")]
        public string OSVersion { get; init; }

        public FlashEspCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);
            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.FlashEspAsync(osVersion: string.IsNullOrWhiteSpace(OSVersion) ? null : OSVersion, 
                                       cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

            await Meadow.ResetMeadowAsync(cancellationToken)
                        .ConfigureAwait(false);
        }
    }
}