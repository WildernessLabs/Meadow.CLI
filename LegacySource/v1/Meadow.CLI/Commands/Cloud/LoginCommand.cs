using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.CloudServices;
using Meadow.CLI.Core.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Cloud
{
    [Command("cloud login", Description = "Log into the Meadow Service")]
    public class LoginCommand : ICommand
    {
        [CommandOption("host", Description = "Optionally set a host (default is https://www.meadowcloud.co)", IsRequired = false)]
        public string Host { get; set; }
        
        private readonly ILogger<LoginCommand> _logger;
        IdentityManager _identityManager;
        UserService _userService;

        public LoginCommand(ILoggerFactory loggerFactory, IdentityManager identityManager, UserService userService)
        {
            _logger = loggerFactory.CreateLogger<LoginCommand>();
            _identityManager = identityManager;
            _userService = userService;
        }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            var loginResult = await _identityManager.Login(Host, cancellationToken);

            if (loginResult)
            {
                var user = await _userService.GetMe(Host, cancellationToken);
                _logger.LogInformation(user != null
                    ? $"Signed in as {user.Email}"
                    : "There was a problem retrieving your account information.");
            }
        }
    }
}