using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Mono
{
    [Command("mono enable", Description = "Sets mono to run on the Meadow board and then resets it")]
    public class MonoEnableCommand : MeadowSerialPortCommand
    {
        private readonly ILogger<MonoEnableCommand> _logger;
        
        public MonoEnableCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory)
            : base(downloadManager, loggerFactory)
        {
            _logger = LoggerFactory.CreateLogger<MonoEnableCommand>();
        }

        [CommandOption("force",'f', Description = "Send the Mono Enable Command even if Mono is already enabled")]
        public bool Force { get; init; }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.MonoEnable(Force, cancellationToken);
            _logger.LogInformation("Mono Enabled Successfully");
        }
    }
}