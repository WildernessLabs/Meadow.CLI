using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Qspi
{
    [Command("qspi write", Description = "Write a QSPI value to the Meadow")]
    public class QspiWriteCommand : MeadowSerialCommand
    {
        private readonly ILogger<QspiWriteCommand> _logger;

        [CommandOption("value",'v', Description = "The QSPI Value to write", IsRequired = true)]
#if WIN_10
        public int Value { get; }
#else
        public int Value {get; init;}
#endif

        public QspiWriteCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<QspiWriteCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.QspiWrite(Value, cancellationToken);
        }
    }
}
