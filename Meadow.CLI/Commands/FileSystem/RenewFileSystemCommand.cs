using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.FileSystem
{
    [Command("filesystem renew", Description = "Create a File System on the Meadow Board")]
    public class RenewFileSystemCommand : MeadowSerialCommand
    {
        public RenewFileSystemCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            await console.Output.WriteLineAsync("Renewing file system on the Meadow.");

            await Meadow.RenewFileSystemAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}