using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine.Commands.DeviceManagement
{
    [Command("flash esp", Description = "Flash the ESP co-processor")]
    public class FlashEspCommand : MeadowSerialCommand
    {
        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device = await MeadowDeviceManager
                                     .GetMeadowForSerialPort(
                                         SerialPortName,
                                         true,
                                         cancellationToken)
                                     .ConfigureAwait(false);

            await Utils.FlashEsp(device, cancellationToken)
                       .ConfigureAwait(false);

            await Utils.ResetMeadow(device, cancellationToken)
                       .ConfigureAwait(false);
        }

        internal FlashEspCommand(ILoggerFactory loggerFactory,
                                 Utils utils,
                                 MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, utils, meadowDeviceManager)
        {
        }
    }
}