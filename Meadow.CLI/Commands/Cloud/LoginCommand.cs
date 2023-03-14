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
        IdentityManager _identityManager;

        public LoginCommand(ILoggerFactory loggerFactory, IdentityManager identityManager)
        {
            _logger = loggerFactory.CreateLogger<LoginCommand>();
            _identityManager = identityManager;
        }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            var loginResult = await _identityManager.Login(cancellationToken);

            if (loginResult)
            {
                var cred = _identityManager.GetCredentials(_identityManager.WlRefreshCredentialName);
                _logger.LogInformation($"Signed in as {cred.username}");
            }
        }
    }
}