using CliFx;
using CliFx.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseCommand<T> : ICommand
{
    protected ILogger<T>? Logger { get; }
    protected ILoggerFactory? LoggerFactory { get; }
    protected IConsole? Console { get; private set; }

    public BaseCommand(ILoggerFactory? loggerFactory)
    {
        LoggerFactory = loggerFactory;
        Logger = loggerFactory?.CreateLogger<T>();
    }

    protected abstract ValueTask ExecuteCommand(CancellationToken? cancellationToken);

    public virtual async ValueTask ExecuteAsync(IConsole console)
    {
        Console = console;
        var cancellationToken = Console?.RegisterCancellationHandler();

        try
        {
            if (cancellationToken != null)
                await ExecuteCommand(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex.Message);
        }
    }
}