using System;
using System.Linq;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Files
{
    [Command("file list", Description = "List files in the on-board filesystem")]
    public class ListFilesCommand : MeadowSerialCommand
    {
#if USE_PARTITIONS
        [CommandOption("Partition", 'p', Description = "The partition to list the files")]
#endif
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

            var files = await Meadow.GetFilesAndCrcsAsync(
                                        TimeSpan.FromSeconds(30),
                                        Partition,
                                        cancellationToken)
                                    .ConfigureAwait(false);

            if (files.Any())
            {

                var longestFileName = files.Keys.Select(x => x.Length)
                                           .OrderByDescending(x => x)
                                           .FirstOrDefault();

                if (IncludeCrcs)
                {
                    foreach (var file in files)
                        _logger.LogInformation(
                            $"{file.Key.PadRight(longestFileName)}\t{file.Value:x8}");
                }
                else
                {
                    foreach (var file in files)
                        _logger.LogInformation($"{file.Key}");
                }
            }
            else
            {
                _logger.LogInformation("No files found.");
            }
        }
    }
}