using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using MeadowCLI.DeviceManagement;

namespace Meadow.CommandLine.Commands.Mono
{
    [Command("mono enable", Description = "Enable Mono on the Meadow Board")]
    public class MonoEnableCommand : MeadowSerialCommand
    {
        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device =
                await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName).ConfigureAwait(false);

            await Utils.EnableMono(console, device, cancellationToken).ConfigureAwait(false);
        }
    }
}
