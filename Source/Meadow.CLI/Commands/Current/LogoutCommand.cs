using CliFx.Attributes;
using Meadow.Cloud.Client.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("logout", Description = "Log out of your Wilderness Labs account")]
public class LogoutCommand : BaseCommand<LogoutCommand>
{
    private readonly IdentityManager _identityManager;

    public LogoutCommand(
        IdentityManager identityManager,
        ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _identityManager = identityManager;
    }

    protected override ValueTask ExecuteCommand()
    {
        Logger.LogInformation($"Logging out of your Wilderness Labs account...");

        _identityManager.Logout();

        return ValueTask.CompletedTask;
    }
}