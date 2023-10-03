using Meadow.Cloud;
using Meadow.Cloud.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseCloudCommand<T> : BaseCommand<T>
{
    protected IdentityManager IdentityManager { get; }
    protected UserService UserService { get; }
    protected DeviceService DeviceService { get; }
    protected CollectionService CollectionService { get; }

    public BaseCloudCommand(
        IdentityManager identityManager,
        UserService userService,
        DeviceService deviceService,
        CollectionService collectionService,
        ILoggerFactory? loggerFactory)
        : base(loggerFactory)
    {
        IdentityManager = identityManager;
        UserService = userService;
        DeviceService = deviceService;
        CollectionService = collectionService;
    }
}
