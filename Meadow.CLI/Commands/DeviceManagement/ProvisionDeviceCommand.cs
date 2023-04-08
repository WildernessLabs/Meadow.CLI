using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.Identity;
using Microsoft.Extensions.Logging;
using Meadow.CLI.Commands;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.IdentityModel.Tokens;
using Meadow.CLI.Core.CloudServices;
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics;
using Meadow.CLI.Core.Exceptions;
using Microsoft.Extensions.Configuration;

namespace Meadow.CommandLine.Commands.Cloud
{
    [Command("device provision", Description = "Registers and prepares connected device for use with Meadow Cloud")]
    public class ProvisionDeviceCommand : MeadowSerialCommand
    {
        DeviceService _deviceService;
        UserService _userService;
        private readonly ILogger<ProvisionDeviceCommand> _logger;
        IConfiguration _config;

        public ProvisionDeviceCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager, IConfiguration config, DeviceService deviceService, UserService userService)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<ProvisionDeviceCommand>();
            _config = config;
            _deviceService = deviceService;
            _userService = userService;
        }

        [CommandOption("orgId", 'o', Description = "The target org for device registration", IsRequired = false)]
        public string OrgId { get; set; }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);
            var cancellationToken = console.RegisterCancellationHandler();

            try
            {
                var userOrgs = await _userService.GetUserOrgs(cancellationToken).ConfigureAwait(false);
                if (!userOrgs.Any())
                {
                    _logger.LogInformation($"Please visit {_config["meadowCloudHost"]} to register your account.");
                    return;
                }
                else if (userOrgs.Count() > 1 && string.IsNullOrEmpty(OrgId))
                {
                    _logger.LogInformation($"Please specify the orgId for this device provisioning.");
                    return;
                }
                else if (userOrgs.Count() == 1 && string.IsNullOrEmpty(OrgId))
                {
                    OrgId = userOrgs.First().OrgId;
                }

                if (!userOrgs.Select(x => x.OrgId).Contains(OrgId))
                {
                    _logger.LogInformation($"Invalid orgId: {OrgId}");
                    return;
                }
            }
            catch (MeadowCloudAuthException)
            {
                _logger.LogInformation($"You must be signed in to execute this command.");
                return;
            }

            using var device = await MeadowDeviceManager.GetMeadowForSerialPort(this.SerialPortName, true, _logger);
            var publicKey = await device.CloudRegisterDevice(cancellationToken);
            var delim = "-----END PUBLIC KEY-----\n";
            publicKey = publicKey.Substring(0, publicKey.IndexOf(delim) + delim.Length);

            var result = await _deviceService.AddDevice(OrgId, device.DeviceInfo.ProcessorId, publicKey);

            if (result.isSuccess)
            {
                _logger.LogInformation("Device provisioned successfully");
            }
            else
            {
                _logger.LogInformation(result.message);
            }

            return;
        }
    }
}
