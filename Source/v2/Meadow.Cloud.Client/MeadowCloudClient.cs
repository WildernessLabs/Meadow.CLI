using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using Meadow.Cloud.Client.Firmware;
using Meadow.Cloud.Client.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.Cloud.Client;

public interface IMeadowCloudClient
{
    IFirmwareClient Firmware { get; }

    Task AuthenticateAsync(string? host = default, CancellationToken cancellationToken = default);
}

public class MeadowCloudClient : IMeadowCloudClient
{
    public const string DefaultHost = "https://www.meadowcloud.co";

    private readonly Lazy<FirmwareClient> _firmwareClient;
    private readonly HttpClient _httpClient;
    private readonly IdentityManager _identityManager;
    private readonly ILogger _logger;
    
    public MeadowCloudClient(HttpClient httpClient, IdentityManager identityManager, MeadowCloudUserAgent userAgent, ILoggerFactory loggerFactory)
    {
        _firmwareClient = new Lazy<FirmwareClient>(() => new FirmwareClient(httpClient));

        _httpClient = httpClient;
        _identityManager = identityManager;
        _logger = loggerFactory.CreateLogger<MeadowCloudClient>();

        _httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
    }

    public IFirmwareClient Firmware => _firmwareClient.Value;

    public async Task AuthenticateAsync(string? host = default, CancellationToken cancellationToken = default)
    {
        host ??= DefaultHost;

        _logger.LogInformation($"Authenticating with Meadow.Cloud{(host != DefaultHost ? $" ({host.ToLowerInvariant()})" : string.Empty)}...");

        var token = await _identityManager.GetAccessToken(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new NotImplementedException();
        }

        _httpClient.BaseAddress = new Uri(host);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}

public class MeadowCloudUserAgent
{
    public static readonly MeadowCloudUserAgent Cli = new ("Meadow.Cli");
    public static readonly MeadowCloudUserAgent Workbench = new ("Meadow.Workbench");

    public string UserAgent { get; }

    public MeadowCloudUserAgent(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            throw new ArgumentException($"'{nameof(userAgent)}' cannot be null or whitespace.", nameof(userAgent));
        }

        UserAgent = userAgent;
    }

    public override string ToString() => UserAgent;
    public override int GetHashCode() => UserAgent.GetHashCode();

    public static implicit operator string(MeadowCloudUserAgent userAgent) => userAgent.UserAgent;
    public static implicit operator MeadowCloudUserAgent(string userAgent) => new (userAgent);
}
