namespace Meadow.CLI;

public enum CommandExitCode
{
    Success = 0,
    GeneralError = 1,
    UserCancelled = 2,
    FileNotFound = 3,
    DirectoryNotFound = 4,
}