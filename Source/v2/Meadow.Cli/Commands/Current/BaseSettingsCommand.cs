using CliFx.Infrastructure;
using Meadow.CLI;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseSettingsCommand<T> : BaseCommand<T>
{
    protected ISettingsManager SettingsManager { get; }

    public BaseSettingsCommand(ISettingsManager settingsManager, ILoggerFactory? loggerFactory)
        : base (loggerFactory)
    {
        SettingsManager = settingsManager;
    }
}