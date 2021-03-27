using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.Auth;

namespace Meadow.CommandLine.Commands.Cloud
{
    [Command("cloud logout", Description = "Logout of the Meadow Service")]
    public class LogoutCommand : ICommand
    {
        public async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            var identityManager = new IdentityManager();
            identityManager.Logout();

            await console.Output.WriteLineAsync($"Signed out of Meadow Service")
                         .ConfigureAwait(false);
        }
    }
}