using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.Telemetry;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Reflection;

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

            try
            {
                if (MeadowTelemetry.Current.ShouldAskForConsent)
                {
                    AnsiConsole.MarkupLine(Strings.Telemetry.ConsentMessage, "[bold]meadow telemetry [[enable|disable]][/]", $"[bold]{MeadowTelemetry.TelemetryEnvironmentVariable}[/]");

                    var result = AnsiConsole.Confirm(Strings.Telemetry.AskToParticipate, defaultValue: true);
                    MeadowTelemetry.Current.SetTelemetryEnabled(result);
                }

                MeadowTelemetry.Current.TrackCommand(GetCommandName());
            }
            catch
            {
                // Swallow any telemetry-related exceptions
            }

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

    private string? GetCommandName()
    {
        return GetType().GetCustomAttribute<CommandAttribute>(true)?.Name;
    }
}