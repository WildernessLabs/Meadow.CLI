using CliFx.Attributes;
using Meadow.Cloud.Client;
using Meadow.Cloud.Client.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud login", Description = "Log in to Meadow.Cloud")]
public class CloudLoginCommand : BaseCloudCommand<CloudLoginCommand>
{
    [CommandOption("host", Description = $"Optionally set a host (default is {DefaultHost})", IsRequired = false)]
    public string? Host { get; set; }

    public CloudLoginCommand(
        IdentityManager identityManager,
        UserService userService,
        DeviceService deviceService,
        CollectionService collectionService,
        ILoggerFactory? loggerFactory)
        : base(identityManager, userService, deviceService, collectionService, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        Host ??= DefaultHost;

        Logger?.LogInformation($"Logging into {Host}...");

        var loginResult = await IdentityManager.Login(Host, CancellationToken);

        if (loginResult)
        {
            var user = await UserService.GetMe(Host, CancellationToken);
            Logger?.LogInformation(user != null
                ? $"Signed in as {user.Email}"
                : "There was a problem retrieving your account information.");
        }
    }
}