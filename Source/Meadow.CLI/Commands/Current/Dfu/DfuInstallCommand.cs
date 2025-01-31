using CliFx.Attributes;
using Meadow.CLI.Core.Internals.Dfu;
using Meadow.Software;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("dfu install", Description = "Install dfu-util to the host operating system")]
public class DfuInstallCommand : BaseSettingsCommand<AppDeployCommand>
{
    public const string DefaultVersion = "0.11";

    [CommandOption("version", 'v', IsRequired = false)]
    public string? Version { get; set; }

    protected DfuInstallCommand(ISettingsManager settingsManager, ILoggerFactory loggerFactory, string version)
         : base(settingsManager, loggerFactory)
    {
        Version = version;
    }

    public DfuInstallCommand(ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(settingsManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        Version ??= DefaultVersion;

        switch (Version)
        {
            case "0.10":
            case "0.11":
                // valid
                break;
            default:
                Logger?.LogError("Only DFU versions 0.10 and 0.11 are supported");
                return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (IsAdministrator())
            {
                try
                {
                    await DfuUtils.InstallDfuUtil(FileManager.WildernessTempFolderPath, Version, CancellationToken);
                }
                catch (Exception ex)
                {
                    throw new CommandException($"Failed to install DFU {Version}: " + ex.Message);
                }
                Logger?.LogInformation($"DFU {Version} installed successfully");
            }
            else
            {
                Logger?.LogError("To install DFU on Windows, you'll need to run the command as an Administrator");
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Logger?.LogWarning("To install DFU on macOS, run: brew install dfu-util");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Logger?.LogWarning("To install DFU on Linux, use the package manager to install the dfu-util package");
        }
    }

    private static bool IsAdministrator()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        else
        {
            return false;
        }
    }
}