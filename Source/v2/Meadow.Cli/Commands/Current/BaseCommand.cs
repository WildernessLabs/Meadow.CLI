using CliFx;
using CliFx.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseCommand<T> : ICommand
{
    protected ILogger<T>? Logger { get; }
    protected ILoggerFactory? LoggerFactory { get; }
    protected IConsole? Console { get; private set; }
    protected CancellationToken CancellationToken { get; private set; }

    public BaseCommand(ILoggerFactory? loggerFactory)
    {
        LoggerFactory = loggerFactory;
        Logger = loggerFactory?.CreateLogger<T>();
    }

    protected abstract ValueTask ExecuteCommand();

    public virtual async ValueTask ExecuteAsync(IConsole console)
    {
        try
        {
            SetConsole(console);

            await ExecuteCommand();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex.Message);
        }
    }

    protected void SetConsole(IConsole console)
    {
        Console = console;
        CancellationToken = Console.RegisterCancellationHandler();
    }
}