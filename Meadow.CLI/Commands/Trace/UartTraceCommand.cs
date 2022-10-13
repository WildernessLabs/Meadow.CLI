using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Trace
{
    [Command("uart trace", Description = "Configure trace logs to go to UART")]
    public class UartTraceCommand : MeadowSerialCommand
    {
        private readonly ILogger<UartTraceCommand> _logger;

        public UartTraceCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory)
            : base(downloadManager, loggerFactory)
        {
            _logger = LoggerFactory.CreateLogger<UartTraceCommand>();
        }

        [CommandOption("enable", 'e', Description = "Enable trace output to UART")]
        public bool Enable { get; set; } = false;

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            if (Enable)
            {
                await Meadow.Uart1Trace(cancellationToken);

                _logger.LogInformation("Sending trace output to UART");
            }
            else
            {
                await Meadow.Uart1Apps(cancellationToken);
                _logger.LogInformation("Sending App output to UART");
            }
        }
    }
}
