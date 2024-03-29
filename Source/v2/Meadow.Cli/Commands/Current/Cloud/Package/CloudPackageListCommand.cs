﻿using CliFx.Attributes;
using Meadow.Cloud.Client;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud package list", Description = "Lists all Meadow Packages (MPAK)")]
public class CloudPackageListCommand : BaseCloudCommand<CloudPackageListCommand>
{
    private readonly PackageService _packageService;

    [CommandOption("orgId", 'o', Description = "Optional organization ID", IsRequired = false)]
    public string? OrgId { get; init; }

    public CloudPackageListCommand(
        IMeadowCloudClient meadowCloudClient,
        PackageService packageService,
        ILoggerFactory loggerFactory)
        : base(meadowCloudClient, loggerFactory)
    {
        _packageService = packageService;
    }

    protected override async ValueTask ExecuteCloudCommand()
    {
        var org = await GetOrganization(OrgId, CancellationToken);

        if (org == null) { return; }

        var packages = await _packageService.GetOrgPackages(org.Id, Host, CancellationToken);

        if (packages == null || packages.Count == 0)
        {
            Logger.LogInformation("No packages found");
        }
        else
        {
            Logger.LogInformation("packages:");
            foreach (var package in packages)
            {
                Logger.LogInformation($" {package.Id} | {package.Name}");
            }
        }
    }
}