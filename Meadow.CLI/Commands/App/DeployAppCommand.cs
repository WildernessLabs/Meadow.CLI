using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.App
{
    [Command("app deploy", Description = "Deploy the specified app to the Meadow")]
    public class DeployAppCommand : MeadowSerialCommand
    {
        [CommandOption(
            "file",
            'f',
            Description = "The path to the application to deploy to the app",
            IsRequired = true)]
        public string File { get; init; }

        [CommandOption("includePdbs", 'i', Description = "Include the PDB files on deploy to enable debugging", IsRequired = false)]
        public bool IncludePdbs { get; init; }

        public DeployAppCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();
            using var device = await MeadowDeviceManager
                                     .GetMeadowForSerialPort(
                                         SerialPortName,
                                         cancellationToken)
                                     .ConfigureAwait(false);

            await device.DeployAppAsync(File, IncludePdbs, cancellationToken)
                  .ConfigureAwait(false);
        }
    }
}
