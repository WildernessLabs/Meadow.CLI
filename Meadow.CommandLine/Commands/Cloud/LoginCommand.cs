using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.Auth;

namespace Meadow.CommandLine.Commands.Cloud
{
    [Command("cloud login", Description = "Log into the Meadow Service")]
    public class LoginCommand : ICommand
    {
        public async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            var identityManager = new IdentityManager();
            var loginResult = await identityManager.LoginAsync().ConfigureAwait(false);
            if (loginResult)
            {
                var cred = identityManager.GetCredentials(identityManager.WLRefreshCredentialName);
                await console.Output.WriteLineAsync($"Signed in as {cred.username}").ConfigureAwait(false);
            }
        }
    }
}
