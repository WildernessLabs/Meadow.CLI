using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using MeadowCLI.DeviceManagement;

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

            await MeadowDeviceManager.MonoRunState(device)
                                     .ConfigureAwait(false);
        }
    }
}