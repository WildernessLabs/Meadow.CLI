using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses;
using Microsoft.Extensions.Logging;
using MonoLibUsb.Descriptors;

namespace Meadow.CLI.Commands.Utility
{
    [Command("debug", Description = "Debug a Meadow Application")]
    public class DebugCommand : MeadowSerialCommand
    {
        private readonly ILogger<InstallDfuUtilCommand> _logger;

        public DebugCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = loggerFactory.CreateLogger<InstallDfuUtilCommand>();
        }

        // VS 2019 - 4024
        // VS 2017 - 4022
        // VS 2015 - 4020
        [CommandOption("DebugPort", 'p', Description = "The port to run the debug server on")]
        public int Port { get; init; } = 4024;

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device = await MeadowDeviceManager
                                     .GetMeadowForSerialPort(
                                         SerialPortName,
                                         cancellationToken)
                                     .ConfigureAwait(false);

            // TODO: This is a terrible hack to link the DataProcessor to the Device
            device.DataProcessor.ForwardDebuggingData = device.ForwardMonoDataToVisualStudioAsync;
            var server = new DebuggingServer(device, Port, _logger);
            await server.StartListening(cancellationToken).ConfigureAwait(false);
        }
    }
}