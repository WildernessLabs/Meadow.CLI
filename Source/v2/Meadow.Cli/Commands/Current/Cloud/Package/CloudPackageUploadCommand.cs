using CliFx.Attributes;
using Meadow.Cloud;
using Meadow.Cloud.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud package upload", Description = "Upload a Meadow Package (MPAK) to Meadow.Cloud")]
public class CloudPackageUploadCommand : BaseCloudCommand<CloudPackageUploadCommand>
{
    [CommandParameter(0, Name = "MpakPath", Description = "The full path of the mpak file", IsRequired = true)]
    public string MpakPath { get; init; }

    [CommandOption("orgId", 'o', Description = "OrgId to upload to", IsRequired = false)]
    public string? OrgId { get; set; }

    [CommandOption("description", 'd', Description = "Description of the package", IsRequired = false)]
    public string? Description { get; set; }

    [CommandOption("host", Description = "Optionally set a host (default is https://www.meadowcloud.co)", IsRequired = false)]
    public string? Host { get; set; }

    private PackageService _packageService;

    public CloudPackageUploadCommand(
        IdentityManager identityManager,
        UserService userService,
        DeviceService deviceService,
        CollectionService collectionService,
        PackageService packageService,
        ILoggerFactory? loggerFactory)
        : base(identityManager, userService, deviceService, collectionService, loggerFactory)
    {
        _packageService = packageService;
    }

    protected override async ValueTask ExecuteCommand()
    {
        if (!File.Exists(MpakPath))
        {
            Logger?.LogError($"Package {MpakPath} does not exist");
            return;
        }

        if (Host == null) Host = DefaultHost;
        var org = await ValidateOrg(Host, OrgId, CancellationToken);

        if (org == null) return;

        try
        {
            Logger?.LogInformation($"Uploading package {Path.GetFileName(MpakPath)}...");

            var package = await _packageService.UploadPackage(MpakPath, org.Id, Description, Host, CancellationToken);
            Logger?.LogInformation($"Upload complete. Package Id: {package.Id}");
        }
        catch (MeadowCloudException mex)
        {
            Logger?.LogError($"Upload failed: {mex.Message}");
        }

    }
}
