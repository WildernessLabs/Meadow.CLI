using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Cloud
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

            var identityManager = new IdentityManager(_logger);
            var loginResult = await identityManager.Login(cancellationToken);

            if (loginResult)
            {
                var cred = identityManager.GetCredentials(identityManager.WlRefreshCredentialName);
                _logger.LogInformation($"Signed in as {cred.username}");
            }
        }
    }
}