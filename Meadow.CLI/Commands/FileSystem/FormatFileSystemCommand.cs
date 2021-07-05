using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.FileSystem
{
    [Command("fs format", Description = "Format a File System on the Meadow Board")]
    public class FormatFileSystemCommand : MeadowSerialCommand
    {
#if USE_PARTITIONS
        [CommandOption("Partition", 'p', Description = "The partition to write to on the Meadow")]
#endif
        public int Partition { get; init; } = 0;

        private readonly ILogger<FormatFileSystemCommand> _logger;

        public FormatFileSystemCommand(ILoggerFactory loggerFactory,
                                       MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<FormatFileSystemCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            _logger.LogInformation($"Formatting file system on partition {Partition}");

            await Meadow.FormatFileSystemAsync(cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
        }
    }
}