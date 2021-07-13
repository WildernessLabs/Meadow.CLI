using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Trace
{
    [Command("trace disable", Description = "Disable Trace Logging on the Meadow")]
    public class TraceDisableCommand : MeadowSerialCommand
    {
        private readonly ILogger<TraceDisableCommand> _logger;

        public TraceDisableCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
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
