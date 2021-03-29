using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine.Commands.Mono
{
    [Command("mono state", Description = "Get the Mono Run State on the Meadow Board")]
    public class MonoRunStateCommand : MeadowSerialCommand
    {
        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, true, cancellationToken)
                                                        .ConfigureAwait(false);

            await device.GetMonoRunState(cancellationToken)
                                     .ConfigureAwait(false);
        }

        internal MonoRunStateCommand(ILoggerFactory loggerFactory, Utils utils, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, utils, meadowDeviceManager)
        {
        }
    }
}