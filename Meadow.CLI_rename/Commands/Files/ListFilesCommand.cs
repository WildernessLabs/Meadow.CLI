using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Meadow.CLI.Commands.Files
{
    [Command("file list", Description = "List files in the on-board filesystem")]
    public class ListFilesCommand : MeadowSerialCommand
    {
#if USE_PARTITIONS
        [CommandOption("Partition", 'p', Description = "The partition to list the files")]
#endif
        public const int FileSystemBlockSize = 4096;

        public int Partition { get; init; } = 0;

        [CommandOption("includeCrcs", 'i', Description = "Include the CRCs of the files")]
        public bool IncludeCrcs { get; init; }

        private readonly ILogger<ListFilesCommand> _logger;

        public ListFilesCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<ListFilesCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            _logger.LogInformation($"Getting files on partition {Partition}");

            var files = await Meadow.GetFilesAndCrcs(
                                        TimeSpan.FromSeconds(60),
                                        Partition,
                                        cancellationToken);

            if (files.Any())
            {
                var longestFileName = files.Select(x => x.FileName.Length)
                                           .OrderByDescending(x => x)
                                           .FirstOrDefault();

                var totalBytesUsed = 0;
                var totalBlocksUsed = 0;

                foreach (var file in files)
                {
                    totalBytesUsed += file.FileSize;
                    totalBlocksUsed += (file.FileSize / FileSystemBlockSize) + 1;

                    var line = $"{file.FileName.PadRight(longestFileName)}";

                    if (IncludeCrcs)
                    {
                        line = $"{line}\t{file.Crc:x8}";
                    }

                    if (file.FileSize > 1000000)
                    {
                        line = $"{line}\t{file.FileSize / 1000000d,7:0.0} MB   ";
                    }
                    else if (file.FileSize > 1000)
                    {
                        line = $"{line}\t{file.FileSize / 1000,7:0} kB   ";
                    }
                    else
                    {
                        line = $"{line}\t{file.FileSize,7} bytes";
                    }

                    _logger.LogInformation(line);
                }

                _logger.LogInformation(
                    $"\nSummary:\n" +
                    $"\t{files.Count} files\n" +
                    $"\t{totalBytesUsed / 1000000d:0.00}MB of file data\n" +
                    $"\tSpanning {totalBlocksUsed} blocks\n" +
                    $"\tConsuming {totalBlocksUsed * FileSystemBlockSize / 1000000d:0.00}MB on disk");
            }
            else
            {
                _logger.LogInformation("No files found.");
            }
        }
    }
}