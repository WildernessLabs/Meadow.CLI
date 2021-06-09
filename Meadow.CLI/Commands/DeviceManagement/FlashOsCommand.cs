using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement
{
    [Command("flash os", Description = "Update the OS on the Meadow Board")]
    public class FlashOsCommand : MeadowSerialCommand
    {
        private readonly ILogger<FlashOsCommand> _logger;

        public FlashOsCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = loggerFactory.CreateLogger<FlashOsCommand>();
        }

        [CommandOption("BinPath", 'b', Description = "Path to the Meadow OS binary")]
        public string BinPath { get; init; }

        [CommandOption("skipDfu",'d', Description = "Skip DFU flash.")]
        public bool SkipDfu { get; init; }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            Meadow?.Dispose();

            await MeadowDeviceManager.FlashOsAsync(SerialPortName, BinPath, SkipDfu, cancellationToken);
        }
    }
}