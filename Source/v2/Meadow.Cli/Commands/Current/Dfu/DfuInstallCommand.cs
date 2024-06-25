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
        Version ??= DfuUtils.DEFAULT_DFU_VERSION;

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

        bool successfullyInstalled = false;
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                successfullyInstalled = await DfuUtils.CheckIfDfuUtilIsInstalledOnWindows(FileManager.WildernessTempFolderPath, Version, CancellationToken);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                successfullyInstalled = await DfuUtils.CheckIfDfuUtilIsInstalledOnMac(Version, CancellationToken);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                successfullyInstalled = await DfuUtils.CheckIfDfuUtilIsInstalledOnLinux(Version, CancellationToken);
            }
        }
        catch (Exception ex)
        {
            throw new CommandException($"Failed to install DFU {Version}: " + ex.Message);
        }

        if (successfullyInstalled)
        {
            Logger?.LogInformation($"DFU Is installed");
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