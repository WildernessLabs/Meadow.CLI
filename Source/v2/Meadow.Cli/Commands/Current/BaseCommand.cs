using CliFx;
using CliFx.Infrastructure;
using Meadow.Cli;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseCommand<T> : ICommand
{
    protected ILogger<T> Logger { get; }
    protected ISettingsManager SettingsManager { get; }

    public BaseCommand(ISettingsManager settingsManager, ILoggerFactory loggerFactory)
    {
        Logger = loggerFactory.CreateLogger<T>();
        SettingsManager = settingsManager;
    }

    protected abstract ValueTask ExecuteCommand(CancellationToken cancellationToken);

    public virtual async ValueTask ExecuteAsync(IConsole console)
    {
        var cancellationToken = console.RegisterCancellationHandler();

        try
        {
            await ExecuteCommand(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
        }
    }
}
