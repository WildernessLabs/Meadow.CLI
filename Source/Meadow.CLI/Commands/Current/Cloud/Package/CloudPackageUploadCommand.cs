using CliFx.Attributes;
using Meadow.Cloud.Client;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud package upload", Description = "Upload a Meadow Package (MPAK) to Meadow.Cloud")]
public class CloudPackageUploadCommand : BaseCloudCommand<CloudPackageUploadCommand>
{
    [CommandParameter(0, Name = "MpakPath", Description = "The full path of the mpak file", IsRequired = true)]
    public string MpakPath { get; init; } = default!;

    [CommandOption("orgId", 'o', Description = "OrgId to upload to", IsRequired = false)]
    public string? OrgId { get; init; }

    [CommandOption("description", 'd', Description = "Description of the package", IsRequired = false)]
    public string? Description { get; init; }

    private readonly PackageService _packageService;

    public CloudPackageUploadCommand(
        IMeadowCloudClient meadowCloudClient,
        PackageService packageService,
        ILoggerFactory loggerFactory)
        : base(meadowCloudClient, loggerFactory)
    {
        _packageService = packageService;
    }
    protected override ValueTask PreAuthenticatedValidation()
    {
        if (!File.Exists(MpakPath))
        {
            throw new CommandException($"Package {MpakPath} does not exist");
        }

        return ValueTask.CompletedTask;
    }

    protected override async ValueTask ExecuteCloudCommand()
    {
        var org = await GetOrganization(OrgId, CancellationToken);

        if (org == null) { return; }

        Logger.LogInformation($"Uploading package {Path.GetFileName(MpakPath)}...");

        var package = await _packageService.UploadPackage(MpakPath, org.Id, Description ?? string.Empty, Host, CancellationToken);
        Logger.LogInformation($"Upload complete. Package Id: {package.Id}");
    }
}