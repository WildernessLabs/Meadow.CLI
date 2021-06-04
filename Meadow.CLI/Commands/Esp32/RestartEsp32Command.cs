using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Esp32
{
    [Command("esp32 restart", Description = "Restart the ESP32")]
    public class RestartEsp32Command : MeadowSerialCommand
    {
        private readonly ILogger<WriteEsp32FileCommand> _logger;

        public RestartEsp32Command(ILoggerFactory loggerFactory,
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

            await device.RestartEsp32Async(cancellationToken).ConfigureAwait(false);
        }
    }
}