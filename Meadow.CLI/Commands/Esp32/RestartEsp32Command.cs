using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Esp32
{
    [Command("esp32 restart", Description = "Restart the ESP32")]
    public class RestartEsp32Command : MeadowSerialCommand
    {
        private readonly ILogger<WriteEsp32FileCommand> _logger;

        public RestartEsp32Command(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<WriteEsp32FileCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.RestartEsp32Async(cancellationToken).ConfigureAwait(false);
        }
    }
}