namespace Meadow.CLI;

public enum CommandExitCode
{
    Success = 0,
    GeneralError = 1,
    UserCancelled = 2,
    FileNotFound = 3,
    DirectoryNotFound = 4,
    MeadowDeviceNotFound = 5,
    InvalidParameter = 6,
    NotAuthorized = 7,
}