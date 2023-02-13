using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.Identity;
using Microsoft.Extensions.Logging;
using Meadow.CLI.Commands;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;

namespace Meadow.CommandLine.Commands.Cloud
{
    [Command("cloud provision device", Description = "Registers and prepares connected device for use with Meadow Cloud")]
    public class CloudRegisterCommand : MeadowSerialCommand
    {
        private readonly ILogger<CloudRegisterCommand> _logger;

        public CloudRegisterCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<CloudRegisterCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);
            var cancellationToken = console.RegisterCancellationHandler();

             using var device =
                await MeadowDeviceManager.GetMeadowForSerialPort(this.SerialPortName, true, _logger);

            var public_key = await device.CloudRegisterDevice(cancellationToken);

            _logger.LogInformation($"Device's public key: {public_key}");

            //TODO: Push public_key PEM string to the Cloud.

            // var identityManager = new IdentityManager();
            // var loginResult = await identityManager.LoginAsync(cancellationToken)
            //                                        .ConfigureAwait(false);

            // if (loginResult)
            // {
            //     var cred = identityManager.GetCredentials(identityManager.WLRefreshCredentialName);
            //     _logger.LogInformation($"Signed in as {cred.username}");
            // }

             _logger.LogInformation("Device provisioned successfully");

            return;
        }
    }
}
