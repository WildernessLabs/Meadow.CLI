using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;

namespace Meadow.CommandLine.Commands.Mono
{
    [Command("mono state", Description = "Get the Mono Run State on the Meadow Board")]
    public class MonoRunStateCommand : MeadowSerialCommand
    {
        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName)
                                                        .ConfigureAwait(false);

            await device.GetMonoRunState(cancellationToken)
                                     .ConfigureAwait(false);
        }
    }
}