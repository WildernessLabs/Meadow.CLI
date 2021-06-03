using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Esp32
{
    [Command("files esp32 write", Description = "Write files to the ESP File System")]
    public class WriteEsp32FileCommand : MeadowSerialCommand
    {
        [CommandOption(
            "file",
            'f',
            Description = "The file to write to the Meadow's ESP32 File System",
            IsRequired = true)]
        public string Filename { get; init; }

        [CommandOption(
            "targetFile",
            't',
            Description = "The filename to use on the Meadow's ESP32 File System")]
        public string TargetFilename { get; init; }

        [CommandOption("McuDestAddress", Description = "Where file is stored in MCU's internal flash e.g. 0x10000", IsRequired = true)]
        public string McuDestAddress { get; init; }

        private readonly ILogger<WriteEsp32FileCommand> _logger;

        public WriteEsp32FileCommand(ILoggerFactory loggerFactory,
                                     MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<WriteEsp32FileCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device = await MeadowDeviceManager
                                     .GetMeadowForSerialPort(
                                         SerialPortName,
                                         cancellationToken)
                                     .ConfigureAwait(false);

            var targetFileName = string.IsNullOrWhiteSpace(TargetFilename)
                                     ? GetTargetFileName()
                                     : TargetFilename;

            _logger.LogInformation(
                $"Writing {Filename} as {targetFileName} to ESP32");

            _logger.LogDebug("Translated {filename} to {targetFileName}", Filename, targetFileName);

            Trace.Assert(
                string.IsNullOrWhiteSpace(targetFileName) == false,
                "string.IsNullOrWhiteSpace(targetFileName)");

            if (!File.Exists(Filename))
            {
                _logger.LogInformation("Cannot find {filename}", Filename);
            }
            else
            {
                await device.WriteFileToEspFlashAsync(Filename, targetFileName, (uint)0, McuDestAddress, cancellationToken)
                            .ConfigureAwait(false);

                _logger.LogDebug("File written successfully");
            }
        }

        private string GetTargetFileName()
        {
            return new FileInfo(Filename).Name;
        }
    }
}