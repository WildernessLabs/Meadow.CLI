using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Files
{
    [Command("file write", Description = "Write files to the Meadow File System")]
    public class WritesFileCommand : MeadowSerialCommand
    {
        [CommandOption(
            "files",
            'f',
            Description = "The file(s) to write to the Meadow Files System",
            IsRequired = true)]
        public IList<string> Files { get; init; }

        [CommandOption(
            "targetFiles",
            't',
            Description = "The filename(s) to use on the Meadow File System")]
        public IList<string> TargetFileNames { get; init; } = Array.Empty<string>();

#if USE_PARTITIONS
        [CommandOption("Partition", 'p', Description = "The partition to write to on the Meadow")]
#endif
        public int Partition { get; init; } = 0;

        private readonly ILogger<WritesFileCommand> _logger;

        public WritesFileCommand(ILoggerFactory loggerFactory,
                                 MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<WritesFileCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            _logger.LogDebug(
                $"{Files.Count} files and {TargetFileNames.Count} target files specified.");

            if (TargetFileNames.Any() && Files.Count != TargetFileNames.Count)
            {
                _logger.LogInformation(
                    $"Number of files to write ({Files.Count}) does not match the number of target file names ({TargetFileNames.Count}).");

                return;
            }

            for (var i = 0; i < Files.Count; i++)
            {
                var targetFileName = GetTargetFileName(i);
                _logger.LogDebug($"Translated {Files[i]} to {targetFileName}");

                System.Diagnostics.Trace.Assert(
                    string.IsNullOrWhiteSpace(targetFileName) == false,
                    "string.IsNullOrWhiteSpace(targetFileName)");

                if (!File.Exists(Files[i]))
                {
                    _logger.LogInformation($"Cannot find {Files[i]}");
                }
                else
                {
                    _logger.LogInformation(
                        $"Writing {Files[i]} as {targetFileName} to partition {Partition}");

                    var result = await Meadow.WriteFileAsync(
                                                 Files[i],
                                                 targetFileName,
                                                 Partition,
                                                 cancellationToken)
                                             .ConfigureAwait(false);

                    _logger.LogDebug($"File written successfully? {result}");
                }
            }
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
}