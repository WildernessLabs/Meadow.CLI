using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.OS
{
    [Command("os update", Description = "Uploads the OS file to the Meadow device and prepares the device for update")]
    public class UpdateOSCommand : MeadowSerialCommand
    {
        [CommandOption("filename",'f', Description = "The local name of the OS file - Default is empty")]
        public string Filename {get; init;}

        private readonly ILogger<UpdateOSCommand> _logger;

        public UpdateOSCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<UpdateOSCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.UpdateOSAsync(Filename, cancellationToken: cancellationToken);

            _logger.LogInformation("OS Updated Successfully");
        }
    }
}