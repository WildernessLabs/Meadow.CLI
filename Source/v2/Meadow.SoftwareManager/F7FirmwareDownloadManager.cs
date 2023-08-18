using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.Software;

internal class F7FirmwareDownloadManager
{
    public event EventHandler<long> DownloadProgress = default!;

    private const string VersionCheckUrlRoot =
           "https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/Meadow_Beta/";

    private readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    public async Task<string> GetLatestAvailableVersion()
    {
        var contents = await GetReleaseMetadata();

        return contents?.Version ?? string.Empty;
    }

    public async Task<F7ReleaseMetadata?> GetReleaseMetadata(string? version = null)
    {
        string versionCheckUrl;
        if (version is null || string.IsNullOrWhiteSpace(version))
        {
            versionCheckUrl = VersionCheckUrlRoot + "latest.json";
        }
        else
        {
            versionCheckUrl = VersionCheckUrlRoot + version + ".json";
        }

        string versionCheckFile;

        try
        {
            versionCheckFile = await DownloadFile(new Uri(versionCheckUrl));
        }
        catch
        {
            return null;
        }

        try
        {
            var content = JsonSerializer.Deserialize<F7ReleaseMetadata>(File.ReadAllText(versionCheckFile));

            return content;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DownloadRelease(string destinationRoot, string version, bool overwrite = false)
    {
        var downloadManager = new F7FirmwareDownloadManager();
        var meta = await downloadManager.GetReleaseMetadata(version);
        if (meta == null) return false;

        CreateFolder(destinationRoot, false);
        //we'll write latest.txt regardless of version if it doesn't exist
        File.WriteAllText(Path.Combine(destinationRoot, "latest.txt"), meta.Version);

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
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

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
