using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Qspi
{
    [Command("qspi read", Description = "Read a QSPI value from the Meadow")]
    public class QspiReadCommand : MeadowSerialCommand
    {
        private readonly ILogger<QspiReadCommand> _logger;

        [CommandOption("value",'v', Description = "The QSPI Value to read", IsRequired = true)]
        public int Value {get; init;}

        public QspiReadCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<QspiReadCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.QspiReadAsync(Value, cancellationToken);
        }
    }
}
