using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Mono
{
    [Command("mono update rt", Description = "Get the Mono Run State on the Meadow Board")]
    public class MonoUpdateRuntimeCommand : MeadowSerialCommand
    {
        [CommandOption("filename",'f', Description = "The local name of the mono runtime file. Default is empty.")]
        public string Filename {get; init;}

        private readonly ILogger<MonoRunStateCommand> _logger;

        public MonoUpdateRuntimeCommand(ILoggerFactory loggerFactory,
                                        MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<MonoRunStateCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device = await MeadowDeviceManager
                                     .GetMeadowForSerialPort(
                                         SerialPortName,
                                         cancellationToken)
                                     .ConfigureAwait(false);

            await device.UpdateMonoRuntime(Filename, cancellationToken: cancellationToken);

            await device.ResetMeadow(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation($"Mono Flashed Successfully");
        }
    }
}