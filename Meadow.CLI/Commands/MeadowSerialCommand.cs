using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands
{
    public abstract class MeadowSerialCommand : ICommand
    {
        private protected ILoggerFactory LoggerFactory;
        private protected MeadowDeviceManager MeadowDeviceManager;

        private protected MeadowSerialCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
        {
            LoggerFactory = loggerFactory;
            MeadowDeviceManager = meadowDeviceManager;
        }

        [CommandOption('v', Description = "Log verbosity")]
        public string[] Verbosity { get; init; }

        [CommandOption("port", 's', Description = "Meadow COM port", IsRequired = true)]
        public string SerialPortName { get; init; }

        [CommandOption("listen", 'k', Description = "Keep port open to listen for output")]
        public bool Listen {get; init;}

        //private protected CancellationToken CancellationToken { get; init; }

        public abstract ValueTask ExecuteAsync(IConsole console);
    }
}
