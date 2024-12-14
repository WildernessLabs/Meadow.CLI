using CliFx.Attributes;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("file delete", Description = "Deletes a file from the device")]
public class FileDeleteCommand : BaseDeviceCommand<FileDeleteCommand>
{
    [CommandParameter(0, Name = "MeadowFile", IsRequired = true)]
    public string MeadowFile { get; init; } = default!;

    public FileDeleteCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();
        var device = await GetCurrentDevice();

        var state = await device.IsRuntimeEnabled(CancellationToken);

        if (state == true)
        {
            Logger?.LogInformation($"{Strings.DisablingRuntime}...");
            await device.RuntimeDisable(CancellationToken);
        }

        if (MeadowFile == "all")
        {
            Logger?.LogInformation($"Looking for files...");
        }
        else
        {
            Logger?.LogInformation($"Looking for file {MeadowFile}...");
        }

        var folder = AppTools.SanitizeMeadowFolderName(Path.GetDirectoryName(MeadowFile)!);

        var fileList = await connection.GetFileList($"{folder}", false, CancellationToken);

        if (fileList == null || fileList.Length == 0)
        {
            Logger?.LogError($"File delete failed, no files found");
            return;
        }

        if (MeadowFile == "all")
        {
            foreach (var file in fileList)
            {
                await DeleteFileRecursive(device, folder, file, CancellationToken);
            }
        }
        else
        {
            var requested = Path.GetFileName(MeadowFile);

            var exists = fileList?.Any(f => Path.GetFileName(f.Name) == requested) ?? false;

            var file = AppTools.SanitizeMeadowFilename(MeadowFile);

            if (!exists)
            {
                Logger?.LogError($"File '{file}' not found on device");
            }
            else
            {
                Logger?.LogInformation($"Deleting '{file}'");
                await device.DeleteFile(file, CancellationToken);
            }
        }
    }

    private async Task DeleteFileRecursive(IMeadowDevice device, string directoryname, MeadowFileInfo fileInfo, CancellationToken cancellationToken)
    {
        var meadowFile = AppTools.SanitizeMeadowFilename(Path.Combine(directoryname, fileInfo.Name));

        foreach (var folder in AppManager.PersistantFolders)
        {
            if (meadowFile.StartsWith($"/{AppManager.MeadowRootFolder}/{folder}"))
            {
                return;
            }
        }

        if (fileInfo.IsDirectory)
        {
            // Add a backslash as we're a directory and not a file
            meadowFile += "/";
            var subfolderFiles = await device.GetFileList(meadowFile, false, cancellationToken);

            foreach (var subfolderFile in subfolderFiles)
            {
                await DeleteFileRecursive(device, meadowFile, subfolderFile, cancellationToken);
            }
            return;
        }

        Logger?.LogInformation($"Deleting file '{meadowFile}' from device...");

        await device.DeleteFile(meadowFile, cancellationToken);
        await Task.Delay(100);
    }
}