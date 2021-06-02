using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine.Commands.Cloud
{
    [Command("cloud login", Description = "Log into the Meadow Service")]
    public class LoginCommand : ICommand
    {
        private readonly ILogger<LoginCommand> _logger;

        public LoginCommand(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<LoginCommand>();
        }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            var identityManager = new IdentityManager();
            var loginResult = await identityManager.LoginAsync(cancellationToken)
                                                   .ConfigureAwait(false);

            if (loginResult)
            {
                var cred = identityManager.GetCredentials(identityManager.WLRefreshCredentialName);
                _logger.LogInformation($"Signed in as {cred.username}");
            }
        }
    }
}