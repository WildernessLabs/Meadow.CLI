using CliFx.Attributes;
using Meadow.Cloud.Client;
using Meadow.Cloud.Client.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("login", Description = "Log in to your Wilderness Labs account")]
public class LoginCommand : BaseCommand<LoginCommand>
{
    private readonly IdentityManager _identityManager;

    public LoginCommand(
        IdentityManager identityManager,
        ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _identityManager = identityManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        Logger.LogInformation($"Logging into your Wilderness Labs account...");

        if (!await _identityManager.Login(CancellationToken))
        {
            throw new CommandException("There was a problem logging into your Wilderness Labs account.");
        }

        var emailAddress = _identityManager.GetEmailAddress();
        Logger.LogInformation($"Signed in as {emailAddress}");
    }
}