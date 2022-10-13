using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Exceptions;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Meadow.CLI.Commands.DeviceManagement
{
    [Command("flash esp", Description = "Flash the ESP co-processor")]
    public class FlashEspCommand : MeadowSerialPortCommand
    {
        [CommandOption("osVersion", 'v', Description = "Flash the ESP from a specific downloaded OS version - x.x.x.x")]
        public string OSVersion { get; init; }

        public FlashEspCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory)
            : base(downloadManager, loggerFactory)
        {
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);
            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.MonoDisable(false, cancellationToken);

            try
            {
                await Meadow.FlashEsp(osVersion: string.IsNullOrWhiteSpace(OSVersion) ? null : OSVersion,
                                           cancellationToken: cancellationToken);
            }
            catch (FileNotFoundException)
            {
                Logger.LogError("Unable to flash ESP: Requested File Not Found");
                return;
            }
            catch (MeadowCommandException mce)
            {
                Logger.LogError($"Unable to flash ESP: Command failed with '{mce.MeadowMessage ?? "no message"}'");
                return;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unable to flash ESP: {ex.Message}");
                return;
            }

            await Meadow.ResetMeadow(cancellationToken);
        }
    }
}