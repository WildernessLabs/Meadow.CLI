using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.Software;

internal class F7FirmwareDownloadManager
{
    private const string VersionCheckUrlPath = "/api/v1/firmware/Meadow_Beta/";

    private readonly HttpClient _client;

    public event EventHandler<long> DownloadProgress = default!;

    public F7FirmwareDownloadManager(HttpClient meadowCloudClient)
    {
        _client = meadowCloudClient;
    }

    public async Task<string> GetLatestAvailableVersion()
    {
        var contents = await GetReleaseMetadata();

        return contents?.Version ?? string.Empty;
    }

    public async Task<F7ReleaseMetadata?> GetReleaseMetadata(string? version = null, CancellationToken? cancellationToken = null)
    {
        version = !string.IsNullOrWhiteSpace(version) ? version : "latest";
        var uri = VersionCheckUrlPath + version;
        var request = new HttpRequestMessage(HttpMethod.Get, uri);

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken ?? CancellationToken.None);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new NotAuthenticatedException($"Access to '{_client.BaseAddress}' not authorized");
            }
            else
            {
                throw new Exception($"Request for release data failed.  Web request returned a {response.StatusCode}");
            }
        }

        return await response.Content.ReadFromJsonAsync<F7ReleaseMetadata>();
    }

    public void SetDefaultVersion(string destinationRoot, string version)
    {
        File.WriteAllText(Path.Combine(destinationRoot, "latest.txt"), version);
    }

    public async Task<bool> DownloadRelease(string destinationRoot, string version, bool overwrite = false)
    {
        var meta = await GetReleaseMetadata(version);
        if (meta == null) return false;

        CreateFolder(destinationRoot, false);

        string local_path;

        if (string.IsNullOrWhiteSpace(version))
        {
            local_path = Path.Combine(destinationRoot, meta.Version);
            version = meta.Version;
        }
        else
        {
            local_path = Path.Combine(destinationRoot, version);
        }

        if (CreateFolder(local_path, overwrite) == false)
        {
            throw new Exception($"Firmware version {version} already exists locally");
        }

        try
        {
            await DownloadAndExtractFile(new Uri(meta.DownloadURL), local_path);
        }
        catch
        {
            throw new Exception($"Unable to download OS files for {version}");
        }

        try
        {
            await DownloadAndExtractFile(new Uri(meta.NetworkDownloadURL), local_path);
        }
        catch
        {
            throw new Exception($"Unable to download Coprocessor files for {version}");
        }

        return true;
    }

    private async Task DownloadAndExtractFile(Uri uri, string target_path, CancellationToken cancellationToken = default)
    {
        var downloadFileName = await DownloadFile(uri, cancellationToken);

        ZipFile.ExtractToDirectory(
            downloadFileName,
            target_path);

        File.Delete(downloadFileName);
    }

    private bool CreateFolder(string path, bool eraseIfExists = true)
    {
        if (Directory.Exists(path))
        {
            if (eraseIfExists)
            {
                CleanPath(path);
            }
            else
            {
                return false;
            }
        }
        else
        {
            Directory.CreateDirectory(path);
        }
        return true;
    }

    private void CleanPath(string path)
    {
        var di = new DirectoryInfo(path);
        foreach (FileInfo file in di.GetFiles())
        {
            file.Delete();
        }
        foreach (DirectoryInfo dir in di.GetDirectories())
        {
            dir.Delete(true);
        }
    }

    private async Task<string> DownloadFile(Uri uri, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        var downloadFileName = Path.GetTempFileName();

        using var stream = await response.Content.ReadAsStreamAsync();

        var contentLength = response.Content.Headers.ContentLength;

        using var downloadFileStream = new DownloadFileStream(stream);
        using var firmwareFile = File.OpenWrite(downloadFileName);

        downloadFileStream.DownloadProgress += (s, e) => { DownloadProgress?.Invoke(this, e); };

        await downloadFileStream.CopyToAsync(firmwareFile);

        return downloadFileName;
    }
}
