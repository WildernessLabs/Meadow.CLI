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
    [Command("file delete", Description = "Delete files from the Meadow File System")]
    public class DeleteFileCommand : MeadowSerialCommand
    {
        [CommandOption(
            "files",
            'f',
            Description = "The file(s) to delete from the Meadow Files System",
            IsRequired = true)]
#if WIN_10
        public IList<string> Files { get; }
#else
        public IList<string> Files { get; init; }
#endif

#if USE_PARTITIONS
        [CommandOption("Partition", 'p', Description = "The partition to write to on the Meadow")]
#endif
#if WIN_10
        public uint Partition { get; } = 0;
#else
        public uint Partition { get; init; } = 0;
#endif

        private readonly ILogger<DeleteFileCommand> _logger;

        public DeleteFileCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<DeleteFileCommand>();
        }


        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            foreach (var file in Files.Where(file => string.IsNullOrWhiteSpace(file) == false))
            {
                if (!string.IsNullOrEmpty(file))
                {
                    _logger.LogInformation($"Deleting {file} from partition {Partition}");

                    await Meadow.DeleteFile(file, Partition, cancellationToken);
                }
            }
        }
    }
}