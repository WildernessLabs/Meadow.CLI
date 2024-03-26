﻿using System.Linq;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine.Commands.Files
{
    [Command("files list", Description = "List files in the on-board filesystem")]
    public class ListFilesCommand : MeadowSerialCommand
    {
#if USE_PARTITIONS
        [CommandOption("Partition", 'p', Description = "The partition to list the files")]
#endif
        public int Partition { get; init; } = 0;

        [CommandOption("includeCrcs", 'i', Description = "Include the CRCs of the files")]
        public bool IncludeCrcs { get; init; }

        private readonly ILogger<ListFilesCommand> _logger;

        public ListFilesCommand(ILoggerFactory loggerFactory,
                                MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<ListFilesCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            _logger.LogInformation($"Getting files on partition {Partition}");

            using var device = await MeadowDeviceManager
                                     .GetMeadowForSerialPort(
                                         SerialPortName,
                                         true,
                                         cancellationToken)
                                     .ConfigureAwait(false);

            var files = await device.GetFilesAndCrcs(
                                        Partition,
                                        cancellationToken: cancellationToken)
                                    .ConfigureAwait(false);

            var longestFileName = files.Keys.Select(x => x.Length).OrderByDescending(x => x)
                                       .FirstOrDefault();

            if (IncludeCrcs)
            {
                foreach (var file in files)
                    _logger.LogInformation($"{file.Key.PadRight(longestFileName)}\t{file.Value:x8}");
            }
            else
            {
                foreach (var file in files)
                    _logger.LogInformation($"{file.Key}");
            }
        }
    }
}