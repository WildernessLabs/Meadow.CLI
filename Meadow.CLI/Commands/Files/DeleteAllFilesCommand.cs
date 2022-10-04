using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Files
{
    [Command("file delete all", Description = "Delete all files from the Meadow File System")]
    public class DeleteAllFilesCommand : MeadowSerialCommand
    {
        public IList<string> Files { get; init; }

#if USE_PARTITIONS
        [CommandOption("Partition", 'p', Description = "The partition to write to on the Meadow")]
#endif
        public int Partition { get; init; } = 0;

        private readonly ILogger<DeleteFileCommand> _logger;

        public DeleteAllFilesCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<DeleteFileCommand>();
        }


        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            _logger.LogInformation($"Getting files on partition {Partition}");

            var files = await Meadow.GetFilesAndCrcsAsync(
                                        TimeSpan.FromSeconds(60),
                                        Partition,
                                        cancellationToken);

            if (files.Any() == false)
            {
                _logger.LogInformation($"No files found on partition {Partition}");
                return;
            }

            foreach (var file in files)
            {
                _logger.LogInformation($"Deleting {file} from partition {Partition}");

                await Meadow.DeleteFileAsync(file.Key, (uint)Partition, cancellationToken);
            }
        }
    }
}