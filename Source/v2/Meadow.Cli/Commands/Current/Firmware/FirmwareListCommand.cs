using System.Linq;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("firmware list", Description = "List locally available firmware")]
public class FirmwareListCommand : BaseCommand<FirmwareListCommand>
{
    private FileManager FileManager { get; }

    public FirmwareListCommand(FileManager fileManager, ILoggerFactory? loggerFactory)
        : base(loggerFactory)
    {
        FileManager = fileManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        await FileManager.Refresh();

        if (Verbose)
        {
            await DisplayVerboseResults(FileManager);
        }
        else
        {
            await DisplayTerseResults(FileManager);
        }
    }

    private async Task DisplayVerboseResults(FileManager manager)
    {
        Logger?.LogInformation($" (D== Default, OSB==OS without bootloader, RT==Runtime, CP==Coprocessor){Environment.NewLine}");
        Logger?.LogInformation($"  D VERSION           OS  OSB  RT  CP  BCL");

        Logger?.LogInformation($"------------------------------------------");

        foreach (var name in manager.Firmware.CollectionNames)
        {
            Logger?.LogInformation($" {name}");
            var collection = manager.Firmware[name.ToString()];

            foreach (var package in collection.OrderByDescending(s=> s.Version))
            {
                if (package == collection.DefaultPackage)
                {
                    Logger?.LogInformation(
                        $"  * {package.Version?.PadRight(18)} " +
                        $"{(package.OSWithBootloader != null ? "X   " : "     ")}" +
                        $"{(package.OsWithoutBootloader != null ? " X   " : "     ")}" +
                        $"{(package.Runtime != null ? "X   " : "    ")}" +
                        $"{(package.CoprocApplication != null ? "X   " : "    ")}" +
                        $"{(package.BclFolder != null ? "X   " : "    ")}"
                        );
                }
                else
                {
                    Logger?.LogInformation(
                        $"    {package.Version?.PadRight(18)} " +
                        $"{(package.OSWithBootloader != null ? "X   " : "     ")}" +
                        $"{(package.OsWithoutBootloader != null ? " X   " : "     ")}" +
                        $"{(package.Runtime != null ? "X   " : "    ")}" +
                        $"{(package.CoprocApplication != null ? "X   " : "    ")}" +
                        $"{(package.BclFolder != null ? "X   " : "    ")}"
                        );
                }
            }

            var update = await collection.UpdateAvailable();
            if (update != null)
            {
                Logger?.LogInformation($"{Environment.NewLine}  ! {update} IS AVAILABLE FOR DOWNLOAD");
            }
        }
    }

    private async Task DisplayTerseResults(FileManager manager)
    {
        foreach (var name in manager.Firmware.CollectionNames)
        {
            Logger?.LogInformation($" {name}");
            var collection = manager.Firmware[name.ToString()];

            foreach (var package in collection.OrderByDescending(s => s.Version))
            {
                if (package == collection.DefaultPackage)
                {
                    Logger?.LogInformation($"  * {package.Version} (default)".ColourConsoleText(ExtensionMethods.ConsoleColourGreen));
                }
                else
                {
                    Logger?.LogInformation($"    {package.Version}");
                }
            }

            var update = await collection.UpdateAvailable();
            if (update != null)
            {
                Logger?.LogInformation($"{Environment.NewLine}  ! {update} IS AVAILABLE FOR DOWNLOAD");
            }
        }
    }
}
