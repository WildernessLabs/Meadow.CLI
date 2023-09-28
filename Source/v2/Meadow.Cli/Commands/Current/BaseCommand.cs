using CliFx;
using CliFx.Infrastructure;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseCommand<T> : ICommand
{
    protected ILogger<T>? Logger { get; }
    protected ILoggerFactory? LoggerFactory { get; }

    public BaseCommand(ILoggerFactory? loggerFactory)
    {
        LoggerFactory = loggerFactory;
        Logger = loggerFactory?.CreateLogger<T>();
    }

    protected abstract ValueTask ExecuteCommand(IConsole console, CancellationToken cancellationToken);

    public virtual async ValueTask ExecuteAsync(IConsole console)
    {
        var cancellationToken = console.RegisterCancellationHandler();

        try
        {
            await ExecuteCommand(console, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex.Message);
        }
    }
}