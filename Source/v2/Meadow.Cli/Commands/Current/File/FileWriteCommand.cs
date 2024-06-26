﻿using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("file write", Description = "Writes one or more files to the device from the local file system")]
public class FileWriteCommand : BaseDeviceCommand<FileWriteCommand>
{
    [CommandOption(
        "files",
        'f',
        Description = "The file(s) to write to the Meadow Files System",
        IsRequired = true)]
    public IList<string> Files { get; init; } = Array.Empty<string>();

    [CommandOption(
        "targetFiles",
        't',
        Description = "The filename(s) to use on the Meadow File System",
        IsRequired = false)]
    public IList<string> TargetFileNames { get; init; } = Array.Empty<string>();

    public FileWriteCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
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

        if (TargetFileNames.Any() && Files.Count != TargetFileNames.Count)
        {
            Logger?.LogError($"Number of files to write ({Files.Count}) does not match the number of target file names ({TargetFileNames.Count}).");

            return;
        }

        connection.FileWriteProgress += (s, e) =>
        {
            var p = e.completed / (double)e.total * 100d;

            if (!double.IsNaN(p))
            {
                // Console instead of Logger due to line breaking for progress bar
                Console?.Output.Write($"Writing  '{e.fileName}': {p:0}%     \r");
            }
        };

        Logger?.LogInformation($"Writing {Files.Count} file{(Files.Count > 1 ? "s" : "")} to device...");

        for (var i = 0; i < Files.Count; i++)
        {
            if (!File.Exists(Files[i]))
            {
                Logger?.LogError($"Cannot find file '{Files[i]}'. Skippping");
            }
            else
            {
                var targetFileName = AppTools.SanitizeMeadowFilename(GetTargetFileName(i));

                Logger?.LogInformation(
                    $"Writing '{Files[i]}' as '{targetFileName}' to device");

                try
                {
                    await device.WriteFile(Files[i], targetFileName, CancellationToken);
                }
                catch (Exception ex)
                {
                    Logger?.LogError($"Error writing file: {ex.Message}");
                }
            }
        }

        //add a black line after writing the file write progress
        Logger?.LogInformation(string.Empty);
    }

    private string GetTargetFileName(int i)
    {
        if (TargetFileNames.Any()
         && TargetFileNames.Count >= i
         && string.IsNullOrWhiteSpace(TargetFileNames[i]) == false)
        {
            return TargetFileNames[i];
        }

        return new FileInfo(Files[i]).Name;
    }
}