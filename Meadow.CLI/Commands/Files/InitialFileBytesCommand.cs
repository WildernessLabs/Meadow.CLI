using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Files
{
    [Command("files initial bytes", Description = "Get the initial bytes from a file")]
    public class InitialFileBytesCommand : MeadowSerialCommand
    {
#if USE_PARTITIONS
        [CommandOption("Partition", 'p', Description = "The partition to list the files")]
#endif
        public uint Partition { get; init; } = 0;

        [CommandOption(
            "file",
            'f',
            Description = "The file to get the bytes from",
            IsRequired = true)]
        public string Filename { get; init; }

        private readonly ILogger<ListFilesCommand> _logger;

        public InitialFileBytesCommand(ILoggerFactory loggerFactory,
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
                                         cancellationToken)
                                     .ConfigureAwait(false);

            var res = await device.GetInitialBytesFromFile(Filename, Partition, cancellationToken);
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