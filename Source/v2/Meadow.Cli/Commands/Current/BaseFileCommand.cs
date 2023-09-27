using Meadow.Cli;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseFileCommand<T> : BaseCommand<T>
{
    protected FileManager FileManager { get; }

    public BaseFileCommand(FileManager fileManager, ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(settingsManager, loggerFactory)
    {
        FileManager = fileManager;
    }
}
