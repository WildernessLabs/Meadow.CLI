using Meadow.CLI;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseFileCommand<T> : BaseSettingsCommand<T>
{
    protected FileManager FileManager { get; }

    public BaseFileCommand(FileManager fileManager, ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(settingsManager, loggerFactory)
    {
        FileManager = fileManager;
    }
}
