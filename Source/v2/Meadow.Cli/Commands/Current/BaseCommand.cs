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
        catch (CommandException ce)
        {
            Logger?.LogError(ce.Message);
            throw;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex.Message);
            throw new CommandException(
                message: ex.Message,
                exitCode: (int)CommandErrors.GeneralError,
                innerException: ex);
        }

        if (CancellationToken.IsCancellationRequested)
        {
            Logger?.LogInformation($"Cancelled");
            throw new CommandException(
                message: "Cancelled",
                exitCode: (int)CommandErrors.UserCancelled);
        }
    }
}