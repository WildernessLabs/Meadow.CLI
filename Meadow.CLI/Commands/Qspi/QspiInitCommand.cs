using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Qspi
{
    [Command("qspi init", Description = "Init the QSPI on the Meadow")]
    public class QspiInitCommand : MeadowSerialCommand
    {
        private readonly ILogger<QspiInitCommand> _logger;

        [CommandOption("value",'v', Description = "The QSPI Value to initialize", IsRequired = true)]
        public int Value {get; init;}

        public QspiInitCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<QspiInitCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.QspiInitAsync(Value, cancellationToken).ConfigureAwait(false);
        }
    }
}
