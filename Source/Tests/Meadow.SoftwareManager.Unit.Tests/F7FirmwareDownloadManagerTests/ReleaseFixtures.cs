using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Meadow.SoftwareManager.Unit.Tests.F7FirmwareDownloadManagerTests;

internal static class ReleaseFixtures
{
    public static string FileUrl(string repository, string tag, string fileName) =>
        $"https://github.com/{repository}/releases/download/{tag}/{fileName}";

    public static string Sha256Of(string text) => Sha256Of(Encoding.UTF8.GetBytes(text));

    public static string Sha256Of(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    public static string Manifest(string repository, string version) =>
        Manifest(repository, version, Encoding.UTF8.GetBytes("os"), Encoding.UTF8.GetBytes("network"));

    public static string Manifest(string repository, string version, byte[] osZip, byte[] networkZip) =>
        $$"""
        {
          "tag": "{{version}}",
          "version": "{{version}}",
          "generation": "3.0",
          "type": "Full",
          "minCLIVersion": "{{version}}",
          "files": {
            "Meadow.OS.zip": { "sha256": "{{Sha256Of(osZip)}}" },
            "Meadow.Network.zip": { "sha256": "{{Sha256Of(networkZip)}}" }
          }
        }
        """;

    public static byte[] Zip(params string[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            foreach (var entry in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(entry).Open());
                writer.Write(entry);
            }
        }

        return stream.ToArray();
    }
}
