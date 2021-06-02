using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.TraceCommands
{
    [Command("trace enable", Description = "Enable trace logging on the Meadow")]
    public class TraceEnableCommand : MeadowSerialCommand
    {
        private readonly ILogger<TraceEnableCommand> _logger;
        public TraceEnableCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<TraceEnableCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device =
                await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, cancellationToken).ConfigureAwait(false);
            
            await device.TraceEnableAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
