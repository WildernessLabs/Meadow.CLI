using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine.Commands.Qspi
{
    [Command("qspi write", Description = "Write a QSPI value to the Meadow")]
    public class QspiWriteCommand : MeadowSerialCommand
    {
        private readonly ILogger<QspiWriteCommand> _logger;

        [CommandOption("value",'v', Description = "The QSPI Value to write", IsRequired = true)]
        public int Value {get; init;}

        public QspiWriteCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<QspiWriteCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device =
                await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, true, cancellationToken).ConfigureAwait(false);
            
            await device.QspiWrite(Value, cancellationToken).ConfigureAwait(false);
        }
    }
}
