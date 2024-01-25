using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("file list", Description = "Lists the files in the current device directory")]
public class FileListCommand : BaseDeviceCommand<FileListCommand>
{
    public const int FileSystemBlockSize = 4096;

    [CommandOption("verbose", 'v', IsRequired = false)]
    public bool Verbose { get; init; }

    [CommandParameter(0, Name = "Folder", IsRequired = false)]
    public string? Folder { get; set; }

    public FileListCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();

        if (connection == null || connection.Device == null)
        {
            return;
        }

        if (Folder != null)
        {
            if (Folder.EndsWith('/') == false)
            {
                Folder += "/";
            }

            Logger?.LogInformation($"Getting file list from '{Folder}'...");
        }
        else
        {
            Logger?.LogInformation($"Getting file list...");
        }

        var files = await connection.Device.GetFileList(Folder ?? "/meadow0/", Verbose, CancellationToken);

        if (files == null || files.Length == 0)
        {
            Logger?.LogInformation($"No files found");
        }
        else
        {
            if (Verbose)
            {
                var longestFileName = files.Select(x => x.Name.Length)
                                                        .OrderByDescending(x => x)
                                                        .FirstOrDefault();

                var totalBytesUsed = 0L;
                var totalBlocksUsed = 0L;

                foreach (var file in files)
                {
                    totalBytesUsed += file.Size ?? 0;
                    totalBlocksUsed += ((file.Size ?? 0) / FileSystemBlockSize) + 1;

                    var line = $"{file.Name.PadRight(longestFileName)}";
                    line = $"{line}\t{file.Crc:x8}";

                    if (file.Size > 1000000)
                    {
                        line = $"{line}\t{file.Size / 1000000d,7:0.0} MB   ";
                    }
                    else if (file.Size > 1000)
                    {
                        line = $"{line}\t{file.Size / 1000,7:0} kB   ";
                    }
                    else
                    {
                        line = $"{line}\t{file.Size,7} bytes";
                    }

                    Logger?.LogInformation(line);
                }

                Logger?.LogInformation(
                    $"\nSummary:\n" +
                    $"\t{files.Length} file(s)\n" +
                    $"\t{totalBytesUsed / 1000000d:0.00}MB of file data\n" +
                    $"\tSpanning {totalBlocksUsed} blocks\n" +
                    $"\tConsuming {totalBlocksUsed * FileSystemBlockSize / 1000000d:0.00}MB on disk");
            }
            else
            {
                foreach (var file in files)
                {
                    Logger?.LogInformation(file.Name);
                }

                Logger?.LogInformation($"\t{files.Length} file(s)");
            }
        }
    }
}