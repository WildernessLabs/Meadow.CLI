﻿using CliFx.Attributes;
using Meadow.Cloud.Client;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud apikey update", Description = "Update a Meadow.Cloud API key")]
public class CloudApiKeyUpdateCommand : BaseCloudCommand<CloudApiKeyUpdateCommand>
{
    [CommandParameter(0, Description = "The name or ID of the API key", IsRequired = true, Name = "NAME_OR_ID")]
    public string NameOrId { get; init; } = default!;

    [CommandOption("name", 'n', Description = "The new name to use for the API key", IsRequired = false)]
    public string? NewName { get; set; }

    [CommandOption("scopes", 's', Description = "The list of scopes (permissions) to grant the API key", IsRequired = false)]
    public string[]? Scopes { get; set; }

    private ApiTokenService ApiTokenService { get; }

    public CloudApiKeyUpdateCommand(
        IMeadowCloudClient meadowCloudClient,
        ApiTokenService apiTokenService,
        ILoggerFactory loggerFactory)
        : base(meadowCloudClient, loggerFactory)
    {
        ApiTokenService = apiTokenService;
    }

    protected override ValueTask PreAuthenticatedValidation()
    {
        Logger.LogInformation($"Updating API key `{NameOrId}` on Meadow.Cloud{(Host != DefaultHost ? $" ({Host.ToLowerInvariant()})" : string.Empty)}...");
        return ValueTask.CompletedTask;
    }

    protected async override ValueTask ExecuteCloudCommand()
    {
        var getRequest = await ApiTokenService.GetApiTokens(Host, CancellationToken);
        var apiKey = getRequest.FirstOrDefault(x => x.Id == NameOrId || string.Equals(x.Name, NameOrId, StringComparison.OrdinalIgnoreCase));

        if (apiKey == null)
        {
            throw new CommandException($"API key `{NameOrId}` not found.");
        }

        NewName ??= apiKey.Name;
        Scopes ??= apiKey.Scopes;

        var updateRequest = new UpdateApiTokenRequest(NewName!, Scopes!);
        await ApiTokenService.UpdateApiToken(apiKey.Id, updateRequest, Host, CancellationToken);
    }
}
