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

    public async ValueTask ExecuteAsync(IConsole console)
    {
        try
        {
            Console = console;
            CancellationToken = Console.RegisterCancellationHandler();

            await ExecuteCommand();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex.Message);
            return;
        }

        if (CancellationToken.IsCancellationRequested)
        {
            Logger?.LogInformation($"Cancelled.");
        }
        else
        {
            Logger?.LogInformation($"Done.");
        }
    }

}