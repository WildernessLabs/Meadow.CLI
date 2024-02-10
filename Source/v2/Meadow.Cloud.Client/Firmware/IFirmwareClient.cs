namespace Meadow.Cloud.Client.Firmware;

public interface IFirmwareClient
{
    Task<IEnumerable<GetFirmwareVersionsResponse>> GetVersions(string type, CancellationToken cancellationToken = default);

    Task<GetFirmwareVersionResponse?> GetVersion(string type, string version, CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> GetDownloadResponse(string url, CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> GetDownloadResponse(Uri url, CancellationToken cancellationToken = default);
}
