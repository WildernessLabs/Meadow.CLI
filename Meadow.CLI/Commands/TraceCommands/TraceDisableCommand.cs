using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.TraceCommands
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
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.TraceDisableAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
