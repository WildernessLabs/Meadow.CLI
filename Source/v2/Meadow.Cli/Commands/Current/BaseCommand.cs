using CliFx;
using CliFx.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseCommand<T> : ICommand
{
    private IConsole? _console;

    protected ILogger<T> Logger { get; }
    protected ILoggerFactory LoggerFactory { get; }
    protected IConsole Console => _console ?? throw new InvalidOperationException("The Console property has not yet been initialized. It can only be used within in the ExecuteCommand() method.");
    protected CancellationToken CancellationToken { get; private set; }

    public BaseCommand(ILoggerFactory loggerFactory)
    {
        LoggerFactory = loggerFactory;
        Logger = loggerFactory.CreateLogger<T>();
    }

    protected abstract ValueTask ExecuteCommand();

    public async ValueTask ExecuteAsync(IConsole console)
    {
        try
        {
            _console = console;
            CancellationToken = _console.RegisterCancellationHandler();

            await ExecuteCommand();
        }
        catch (Exception ex) when (ex is not CommandException && ex is not CliFx.Exceptions.CommandException)
        {
            throw new CommandException(ex.Message, ex);
        }

        if (CancellationToken.IsCancellationRequested)
        {
            throw new CommandException("Cancelled", CommandExitCode.UserCancelled);
        }
    }
}