using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.Cli;
using Meadow.CLI.Core.Internals.Dfu;
using Meadow.Hcom;
using Meadow.Software;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("dfu install", Description = "Deploys a built Meadow application to a target device")]
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
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        if (Version == null)
        {
            Version = DefaultVersion;
        }

        switch (Version)
        {
            case "0.10":
            case "0.11":
                // valid
                break;
            default:
                Logger?.LogError("Only versions 0.10 and 0.11 are supported.");
                return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (IsAdministrator())
            {
                await DfuUtils.InstallDfuUtil(FileManager.WildernessTempFolderPath, Version, CancellationToken);
            }
            else
            {
                Logger?.LogError("To install DFU on Windows, you'll need to re-run the command from as an Administrator");
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Logger?.LogWarning("To install DFU on macOS, run: brew install dfu-util");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Logger?.LogWarning(
                "To install DFU on Linux, use the package manager to install the dfu-util package");
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