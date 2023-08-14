using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Files
{
    [Command("file initial", Description = "Get the initial bytes from a file")]
    public class InitialFileBytesCommand : MeadowSerialCommand
    {
#if USE_PARTITIONS
        [CommandOption("Partition", 'p', Description = "The partition to list the files")]
#endif
#if WIN_10
        public uint Partition { get; } = 0;
#else
        public uint Partition { get; init; } = 0;
#endif

        [CommandOption(
            "file",
            'f',
            Description = "The file to get the bytes from",
            IsRequired = true)]
#if WIN_10
        public string Filename { get; }
#else
        public string Filename { get; init; }
#endif

        private readonly ILogger<ListFilesCommand> _logger;

        public InitialFileBytesCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<ListFilesCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            _logger.LogInformation($"Getting files on partition {Partition}");

            var res = await Meadow.GetInitialBytesFromFile(Filename, Partition, cancellationToken);
            if (res != null)
            {
                _logger.LogInformation("Read {size} bytes from {filename}: {bytes}",
                                       res.Length,
                                       Filename,
                                       res);
            }
            else
            {
                _logger.LogInformation("Failed to retrieve bytes from {filename}", Filename);
            }
        }
    }
}