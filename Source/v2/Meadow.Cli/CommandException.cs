namespace Meadow.CLI;

/// <inheritdoc/>
public class CommandException : CliFx.Exceptions.CommandException
{
    public CommandException(string message)
        : base(message, (int)CommandExitCode.GeneralError, showHelp: false, innerException: null)
    { }

    public CommandException(string message, bool showHelp)
        : base(message, (int)CommandExitCode.GeneralError, showHelp, innerException: null)
    { }

    public CommandException(string message, CommandExitCode exitCode)
        : base(message, (int)exitCode, showHelp: false, innerException: null)
    { }

    public CommandException(string message, CommandExitCode exitCode, bool showHelp)
        : base(message, (int)exitCode, showHelp, innerException: null)
    { }

    public CommandException(string message, Exception innerException)
        : base(message, (int)CommandExitCode.GeneralError, showHelp: false, innerException)
    { }

    public CommandException(string message, bool showHelp, Exception innerException)
        : base(message, (int)CommandExitCode.GeneralError, showHelp, innerException)
    { }

    public CommandException(string message, CommandExitCode exitCode, Exception innerException)
        : base(message, (int)exitCode, showHelp: false, innerException: innerException)
    { }

    public CommandException(string message, CommandExitCode exitCode, bool showHelp, Exception innerException)
        : base(message, (int)exitCode, showHelp, innerException: innerException)
    { }
}