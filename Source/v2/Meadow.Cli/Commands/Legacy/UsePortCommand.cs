using CliFx.Attributes;
using Meadow.CLI;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("use port", Description = "** Deprecated ** Use `config route` instead")]
public class UsePortCommand : BaseCommand<UsePortCommand>
{
    private ISettingsManager _settingsManager;

    [CommandParameter(0, Name = "Port", IsRequired = true)]
    public string Port { get; set; } = default!;

    public UsePortCommand(ILoggerFactory loggerFactory, ISettingsManager settingsManager)
        : base(loggerFactory)
    {
        Logger?.LogWarning($"Deprecated command.  Use `config route` instead");
        _settingsManager = settingsManager;
    }

    protected override ValueTask ExecuteCommand()
    {
        _settingsManager.SaveSetting("route", Port);
        Logger?.LogInformation($"Using {Port}");

        return ValueTask.CompletedTask;
    }
}

