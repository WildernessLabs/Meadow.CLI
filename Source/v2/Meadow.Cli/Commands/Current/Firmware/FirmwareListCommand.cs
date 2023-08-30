using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.Cli;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("firmware list", Description = "List locally available firmware")]
public class FirmwareListCommand : ICommand
{
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<DeviceInfoCommand>? _logger;

    [CommandOption("verbose", 'v', IsRequired = false)]
    public bool Verbose { get; set; }

    public FirmwareListCommand(ISettingsManager settingsManager, ILoggerFactory? loggerFactory)
    {
        _settingsManager = settingsManager;
        _logger = loggerFactory?.CreateLogger<DeviceInfoCommand>();
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {

        var manager = new FileManager();

        await manager.Refresh();

        if (Verbose)
        {
            await DisplayVerboseResults(manager);
        }
        else
        {
            await DisplayTerseResults(manager);
        }
    }

    private async Task DisplayVerboseResults(FileManager manager)
    {
        _logger?.LogInformation($" (D== Default, OSB==OS without bootloader, RT==Runtime, CP==Coprocessor){Environment.NewLine}");
        _logger?.LogInformation($"  D VERSION           OS  OSB  RT  CP  BCL");

        _logger?.LogInformation($"------------------------------------------");

        foreach (var name in manager.Firmware.CollectionNames)
        {
            _logger?.LogInformation($" {name}");
            var collection = manager.Firmware[name];

            foreach (var package in collection)
            {
                if (package == collection.DefaultPackage)
                {
                    _logger?.LogInformation(
                        $"  * {package.Version.PadRight(18)} " +
                        $"{(package.OSWithBootloader != null ? "X   " : "     ")}" +
                        $"{(package.OsWithoutBootloader != null ? " X   " : "     ")}" +
                        $"{(package.Runtime != null ? "X   " : "    ")}" +
                        $"{(package.CoprocApplication != null ? "X   " : "    ")}" +
                        $"{(package.BclFolder != null ? "X   " : "    ")}"
                        );
                }
                else
                {
                    _logger?.LogInformation(
                        $"    {package.Version.PadRight(18)} " +
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
                _logger?.LogInformation($"{Environment.NewLine}  ! {update} IS AVAILABLE FOR DOWNLOAD");
            }
        }
    }

    private async Task DisplayTerseResults(FileManager manager)
    {
        foreach (var name in manager.Firmware.CollectionNames)
        {
            _logger?.LogInformation($" {name}");
            var collection = manager.Firmware[name];

            foreach (var package in collection)
            {
                if (package == collection.DefaultPackage)
                {
                    _logger?.LogInformation($"  * {package.Version} (default)");
                }
                else
                {
                    _logger?.LogInformation($"    {package.Version}");
                }
            }

            var update = await collection.UpdateAvailable();
            if (update != null)
            {
                _logger?.LogInformation($"{Environment.NewLine}  ! {update} IS AVAILABLE FOR DOWNLOAD");
            }
        }
    }
}
