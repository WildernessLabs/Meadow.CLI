using Meadow.Cli;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseFileCommand<T> : BaseSettingsCommand<T>
{
    protected FileManager FileManager { get; }
    protected IFirmwarePackageCollection? Collection { get; private set; }

    public BaseFileCommand(FileManager fileManager, ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(settingsManager, loggerFactory)
    {
        FileManager = fileManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        await FileManager.Refresh();

        // for now we only support F7
        // TODO: add switch and support for other platforms
        Collection = FileManager.Firmware["Meadow F7"];
    }
}