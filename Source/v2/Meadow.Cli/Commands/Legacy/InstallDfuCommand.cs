﻿using CliFx.Attributes;
using Meadow.Cli;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("install dfu", Description = "** deprecated **")]
public class InstallDfuCommand : DfuInstallCommand
{
    public InstallDfuCommand(ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(settingsManager, loggerFactory, "0.11")
    {
        Logger.LogWarning($"Deprecated command.  Use `runtime enable` instead");
    }
}
