using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
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

        public QspiReadCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<QspiReadCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device =
                await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, cancellationToken).ConfigureAwait(false);
            
            await device.QspiRead(Value, cancellationToken).ConfigureAwait(false);
        }
    }
}
