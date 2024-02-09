using CliFx;
using CliFx.Exceptions;
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
            throw new CliFxException(ex.Message, -1);
        }

        if (CancellationToken.IsCancellationRequested)
        {
            Logger?.LogInformation($"Cancelled");
            throw new CliFxException("Cancelled", -2);
        }
    }
}