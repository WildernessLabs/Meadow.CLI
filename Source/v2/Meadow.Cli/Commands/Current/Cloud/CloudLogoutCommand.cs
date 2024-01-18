using CliFx.Attributes;
using Meadow.Cloud;
using Meadow.Cloud.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud logout", Description = "Log out of Meadow.Cloud")]
public class CloudLogoutCommand : BaseCloudCommand<CloudLogoutCommand>
{
    public CloudLogoutCommand(
        IdentityManager identityManager,
        UserService userService,
        DeviceService deviceService,
        CollectionService collectionService,
        ILoggerFactory? loggerFactory)
        : base(identityManager, userService, deviceService, collectionService, loggerFactory)
    { }

    protected override ValueTask ExecuteCommand()
    {
        Logger?.LogInformation($"Logging out of Meadow.Cloud...");

        IdentityManager.Logout();

        return ValueTask.CompletedTask;
    }
}