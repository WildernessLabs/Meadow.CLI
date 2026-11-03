using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.Software;

/// <summary>
/// Retrieves F7 firmware release metadata and packages from the GitHub releases of the Meadow.Releases repository.
/// No authentication is required.
/// </summary>
internal class F7FirmwareDownloadManager
{
    /// <summary>
    /// The default GitHub repository (<c>owner/name</c>) that publishes firmware releases.
    /// Each release carries a <c>manifest.json</c> plus the <c>Meadow.OS.zip</c> and <c>Meadow.Network.zip</c> packages.
    /// </summary>
    public const string DefaultReleasesRepository = "WildernessLabs/Meadow.Releases";

    public const string ManifestFile = "manifest.json";
    public const string OSPackageFile = "Meadow.OS.zip";
    public const string NetworkPackageFile = "Meadow.Network.zip";

    private const string GitHubApiRoot = "https://api.github.com/repos/";
    private const string GitHubRoot = "https://github.com/";

    private static readonly Lazy<HttpClient> s_defaultHttpClient = new(() => new HttpClient());

    private readonly HttpClient _httpClient;
    private readonly string _repository;

    public event EventHandler<long> DownloadProgress = default!;

    public F7FirmwareDownloadManager(HttpClient? httpClient = null, string? releasesRepository = null)
    {
        _httpClient = httpClient ?? s_defaultHttpClient.Value;

        _repository = string.IsNullOrWhiteSpace(releasesRepository)
            ? DefaultReleasesRepository
            : releasesRepository!.Trim().Trim('/');
    }

    public Uri ReleasesApiUri => new($"{GitHubApiRoot}{_repository}/releases?per_page=100");

    public Uri GetReleaseFileUri(string tag, string fileName) =>
        new($"{GitHubRoot}{_repository}/releases/download/{Uri.EscapeDataString(tag)}/{fileName}");

    public async Task<string> GetLatestAvailableVersion()
    {
        var contents = await GetReleaseMetadata();

        return contents?.Version ?? string.Empty;
    }

    /// <summary>
    /// Gets the manifest of a release. With no version, uses the highest-versioned release that is neither a draft nor a pre-release.
    /// </summary>
    public async Task<F7ReleaseMetadata?> GetReleaseMetadata(string? version = null, CancellationToken cancellationToken = default)
    {
        var tag = string.IsNullOrWhiteSpace(version)
            ? await GetLatestReleaseTag(cancellationToken)
            : version!.Trim();

        if (tag == null)
        {
            return null;
        }

        var uri = GetReleaseFileUri(tag, ManifestFile);

        using var response = await _httpClient.GetAsync(uri, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
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

        if (string.IsNullOrWhiteSpace(metadata.Tag))
        {
            metadata.Tag = tag;
        }

        return metadata;
    }

    private async Task<string?> GetLatestReleaseTag(CancellationToken cancellationToken)
    {
        var uri = ReleasesApiUri;

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("Meadow.CLI");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureSuccess(response, uri);

        var json = await response.Content.ReadAsStringAsync();
        var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(json) ?? new();

        return releases
            .Where(r => !r.Draft && !r.Prerelease)
            .Select(r => (r.TagName, Version: System.Version.TryParse(r.TagName, out var v) ? v : null))
            .Where(r => r.Version != null)
            .OrderByDescending(r => r.Version)
            .Select(r => r.TagName)
            .FirstOrDefault();
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
            await DownloadAndExtractFile(meta, OSPackageFile, local_path);
        }
        catch (Exception ex)
        {
            Directory.Delete(local_path, true);
            throw new Exception($"Unable to download OS files for {version}: {ex.Message}");
        }

        try
        {
            await DownloadAndExtractFile(meta, NetworkPackageFile, local_path);
        }
        catch (Exception ex)
        {
            Directory.Delete(local_path, true);
            throw new Exception($"Unable to download Coprocessor files for {version}: {ex.Message}");
        }

        return true;
    }

    private async Task DownloadAndExtractFile(F7ReleaseMetadata meta, string fileName, string target_path, CancellationToken cancellationToken = default)
    {
        if (!meta.Files.TryGetValue(fileName, out var file) || string.IsNullOrWhiteSpace(file.Sha256))
        {
            throw new Exception($"Release manifest has no SHA-256 for {fileName}");
        }

        var downloadFileName = await DownloadFile(GetReleaseFileUri(meta.Tag, fileName), cancellationToken);

        try
        {
            var actual = ComputeSha256(downloadFileName);
            if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"SHA-256 mismatch for {fileName}: expected {file.Sha256}, got {actual}");
            }

            ZipFile.ExtractToDirectory(
                downloadFileName,
                target_path);
        }
        finally
        {
            File.Delete(downloadFileName);
        }
    }

    private static string ComputeSha256(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);

        return string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
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

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = default!;
        [JsonPropertyName("draft")]
        public bool Draft { get; set; }
        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }
    }
}
