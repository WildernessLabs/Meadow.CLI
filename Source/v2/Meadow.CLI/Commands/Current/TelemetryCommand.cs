using CliFx.Attributes;
using CliFx.Extensibility;
using Meadow.Telemetry;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("telemetry", Description = "Manage participation in telemetry sharing")]
public class TelemetryCommand : BaseCommand<TelemetryCommand>
{
    public TelemetryCommand(
        ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
    }

    protected override ValueTask ExecuteCommand()
    {
        throw new CommandException("Specify one of the telemetry commands", true);
    }
}

[Command("telemetry enable", Description = "Enable and opt in to telemetry sharing")]
public class TelemetryEnableCommand : BaseCommand<TelemetryCommand>
{
    private readonly ISettingsManager _settingsManager;

    public TelemetryEnableCommand(
        ISettingsManager settingsManager,
        ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _settingsManager = settingsManager;
    }

    protected override ValueTask ExecuteCommand()
    {
        _settingsManager.SaveSetting(MeadowTelemetry.TelemetryEnabledSettingName, "true");

        return ValueTask.CompletedTask;
    }
}

[Command("telemetry disable", Description = "Disable and opt out of telemetry sharing")]
public class TelemetryDisableCommand : BaseCommand<TelemetryCommand>
{
    private readonly ISettingsManager _settingsManager;

    public TelemetryDisableCommand(
        ISettingsManager settingsManager,
        ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _settingsManager = settingsManager;
    }

    protected override ValueTask ExecuteCommand()
    {
        _settingsManager.SaveSetting(MeadowTelemetry.TelemetryEnabledSettingName, "false");
        _settingsManager.DeleteSetting(MeadowTelemetry.MachineIdSettingName);

        return ValueTask.CompletedTask;
    }
}