using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("use port", Description = "** Deprecated ** Use `port select` (if connecting via serial port) or `config route` (if connecting via IP) instead")]
public class UsePortCommand : BaseCommand<UsePortCommand>
{
    private readonly ISettingsManager _settingsManager;

    [CommandParameter(0, Name = "Port", IsRequired = true)]
    public string Port { get; set; } = default!;

    public UsePortCommand(ILoggerFactory loggerFactory, ISettingsManager settingsManager)
        : base(loggerFactory)
    {
        Logger?.LogWarning($"Deprecated command -use `config route` instead");
        _settingsManager = settingsManager;
    }

    protected override ValueTask ExecuteCommand()
    {
        _settingsManager.SaveSetting("route", Port);
        Logger?.LogInformation($"Using {Port}");

        return ValueTask.CompletedTask;
    }
}