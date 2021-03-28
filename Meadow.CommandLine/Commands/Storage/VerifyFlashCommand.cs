using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;

namespace Meadow.CommandLine.Commands.Storage
{
    [Command("flash verify", Description = "Erase the flash on the Meadow Board")]
    public class VerifyFlashCommand : MeadowSerialCommand
    {
        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            await console.Output.WriteLineAsync("Verifying flash").ConfigureAwait(false);
            using var device = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName).ConfigureAwait(false);
            await device.VerifyErasedFlash(cancellationToken).ConfigureAwait(false);
        }
    }
}
