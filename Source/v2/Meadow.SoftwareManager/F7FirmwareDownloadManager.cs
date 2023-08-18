using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;


namespace Meadow.Software;

internal class DownloadFileStream : Stream, IDisposable
{
    private readonly Stream _stream;

    private long _position;

    public DownloadFileStream(Stream stream)
    {
        _stream = stream;
    }

    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _stream.Length;
    public override long Position { get => _position; set => throw new NotImplementedException(); }

    public override void Flush()
    {
        throw new NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var b = _stream.Read(buffer, offset, count);
        _position += b;
        return b;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        _stream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}

public class ReleaseMetadata
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = default!;
    [JsonPropertyName("minCLIVersion")]
    public string MinCLIVersion { get; set; } = default!;
    [JsonPropertyName("downloadUrl")]
    public string DownloadURL { get; set; } = default!;
    [JsonPropertyName("networkDownloadUrl")]
    public string NetworkDownloadURL { get; set; } = default!;

}

internal class F7FirmwareDownloadManager
{
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

    private async Task<ReleaseMetadata?> GetReleaseMetadata(string? version = null)
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
            var content = JsonSerializer.Deserialize<ReleaseMetadata>(File.ReadAllText(versionCheckFile));

            return content;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> DownloadFile(Uri uri, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        var downloadFileName = Path.GetTempFileName();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var downloadFileStream = new DownloadFileStream(stream);
        using var firmwareFile = File.OpenWrite(downloadFileName);
        await downloadFileStream.CopyToAsync(firmwareFile);

        return downloadFileName;
    }
}
