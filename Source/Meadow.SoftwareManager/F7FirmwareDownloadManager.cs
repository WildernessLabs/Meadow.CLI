using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.Software;

/// <summary>
/// Retrieves F7 firmware release metadata and packages from the public Wilderness Labs download bucket.
/// No Meadow.Cloud account or authentication is required.
/// </summary>
internal class F7FirmwareDownloadManager
{
    /// <summary>
    /// The default (public, unauthenticated) firmware source. Release metadata is published as
    /// <c>latest.json</c> and <c>{version}.json</c> under this root, alongside the package zips.
    /// </summary>
    public const string DefaultFirmwareSourceUrl = "https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/Meadow_Beta/";

    private static readonly Lazy<HttpClient> s_defaultHttpClient = new(() => new HttpClient());

    private readonly HttpClient _httpClient;
    private readonly Uri _sourceRoot;

    public event EventHandler<long> DownloadProgress = default!;

    public F7FirmwareDownloadManager(HttpClient? httpClient = null, string? sourceUrl = null)
    {
        _httpClient = httpClient ?? s_defaultHttpClient.Value;

        var root = string.IsNullOrWhiteSpace(sourceUrl) ? DefaultFirmwareSourceUrl : sourceUrl!.Trim();
        if (!root.EndsWith("/"))
        {
            root += "/";
        }

        _sourceRoot = new Uri(root, UriKind.Absolute);
    }

    public async Task<string> GetLatestAvailableVersion()
    {
        var contents = await GetReleaseMetadata();

        return contents?.Version ?? string.Empty;
    }

    public async Task<F7ReleaseMetadata?> GetReleaseMetadata(string? version = null, CancellationToken cancellationToken = default)
    {
        version = string.IsNullOrWhiteSpace(version) ? "latest" : version!.Trim();
        var uri = new Uri(_sourceRoot, $"{version}.json");

        using var response = await _httpClient.GetAsync(uri, cancellationToken);

        // S3 answers 404 (or 403 when listing is disabled) for a key that does not exist
        if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden)
        {
            return null;
        }

        EnsureSuccess(response, uri);

        var json = await response.Content.ReadAsStringAsync();
        var metadata = JsonSerializer.Deserialize<F7ReleaseMetadata>(json);

        if (metadata == null || string.IsNullOrWhiteSpace(metadata.Version))
        {
            return null;
        }

        return metadata;
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
        catch (Exception ex)
        {
            Directory.Delete(local_path, true);
            throw new Exception($"Unable to download OS files for {version}: {ex.Message}");
        }

        try
        {
            await DownloadAndExtractFile(new Uri(meta.NetworkDownloadURL), local_path);
        }
        catch (Exception ex)
        {
            Directory.Delete(local_path, true);
            throw new Exception($"Unable to download Coprocessor files for {version}: {ex.Message}");
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
        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        EnsureSuccess(response, uri);

        var downloadFileName = Path.GetTempFileName();

        using var stream = await response.Content.ReadAsStreamAsync();

        using var downloadFileStream = new DownloadFileStream(stream);
        using var firmwareFile = File.OpenWrite(downloadFileName);

        downloadFileStream.DownloadProgress += (s, e) => { DownloadProgress?.Invoke(this, e); };

        await downloadFileStream.CopyToAsync(firmwareFile);

        return downloadFileName;
    }

    private static void EnsureSuccess(HttpResponseMessage response, Uri uri)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new HttpRequestException(
            $"Request to '{uri}' failed with status {(int)response.StatusCode} ({response.ReasonPhrase}).");
    }
}
