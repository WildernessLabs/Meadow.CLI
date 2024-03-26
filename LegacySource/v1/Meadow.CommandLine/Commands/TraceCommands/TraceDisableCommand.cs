using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine.Commands.TraceCommands
{
    [Command("trace disable", Description = "Disable Trace Logging on the Meadow")]
    public class TraceDisableCommand : MeadowSerialCommand
    {
        private readonly ILogger<TraceDisableCommand> _logger;
        public TraceDisableCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<TraceDisableCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device =
                await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, true, cancellationToken).ConfigureAwait(false);
            
            await device.TraceDisable(cancellationToken).ConfigureAwait(false);
        }
    }
}
