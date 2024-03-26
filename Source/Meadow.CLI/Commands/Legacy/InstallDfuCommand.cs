using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("install dfu", Description = "** Deprecated ** Use `dfu install` instead")]
public class InstallDfuCommand : DfuInstallCommand
{
    public InstallDfuCommand(ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(settingsManager, loggerFactory, "0.11")
    {
        Logger?.LogWarning($"Deprecated command - use `dfu install` instead");
    }
}