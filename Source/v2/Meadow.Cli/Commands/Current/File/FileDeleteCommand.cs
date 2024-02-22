using CliFx.Attributes;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("file delete", Description = "Deletes a file from the device")]
public class FileDeleteCommand : BaseDeviceCommand<FileDeleteCommand>
{
    [CommandParameter(0, Name = "MeadowFile", IsRequired = true)]
    public string MeadowFile { get; init; } = default!;

    private const string MeadowRootFolder = "meadow0";

    public FileDeleteCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();
        var device = await GetCurrentDevice();

        // get a list of files in the target folder
        var folder = Path.GetDirectoryName(MeadowFile)!.Replace(Path.DirectorySeparatorChar, '/');
        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = MeadowRootFolder;
        }

        var fileList = await connection.GetFileList($"/{folder}/", false);

        if (fileList == null || fileList.Length == 0)
        {
            Logger?.LogError($"File delete failed, no files found");
            return;
        }

        if (MeadowFile == "all")
        {
            foreach (var file in fileList)
            {
                await DeleteFileRecursive(device, file, CancellationToken);
            }
        }
        else
        {
            var requested = Path.GetFileName(MeadowFile);

            var exists = fileList?.Any(f => Path.GetFileName(f.Name) == requested) ?? false;

            if (!exists)
            {
                Logger?.LogError($"File '{MeadowFile}' not found on device.");
            }
            else
            {
                var wasRuntimeEnabled = await device.IsRuntimeEnabled(CancellationToken);

                if (wasRuntimeEnabled)
                {
                    Logger?.LogError($"The runtime must be disabled before doing any file management. Use 'meadow runtime disable' first.");
                    return;
                }

                Logger?.LogInformation($"Deleting file '{MeadowFile}' from device...");
                await device.DeleteFile(MeadowFile, CancellationToken);
            }
        }
    }

    private async Task DeleteFileRecursive(IMeadowDevice device, MeadowFileInfo fileInfo, CancellationToken cancellationToken)
    {
        if (fileInfo.IsDirectory)
        {
            var subfolderFiles = await device.GetFileList(fileInfo.Name, false, cancellationToken);

            foreach (var subfolderFile in subfolderFiles)
            {
                await DeleteFileRecursive(device, subfolderFile, cancellationToken);
            }
            return;
        }

        var fileName = Path.GetFileName(fileInfo.Name);
        Logger?.LogInformation($"Deleting file '{fileName}' from device...");
        await device.DeleteFile(fileName, cancellationToken);
    }
}