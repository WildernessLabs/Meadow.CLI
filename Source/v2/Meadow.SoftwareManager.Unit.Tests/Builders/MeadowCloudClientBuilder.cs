using FakeItEasy;
using Meadow.Software;
using System.Net;
using System.Net.Http.Json;

namespace Meadow.SoftwareManager.Unit.Tests.Builders;

public class MeadowCloudClientBuilder
{
    private readonly Dictionary<(string Type, string Version), F7ReleaseMetadata> _firmware = new();
    private readonly Dictionary<(string Type, string Version), HttpResponseMessage> _firmwareResponses = new();

    public MeadowCloudClientBuilder WithFirmware(string type, string version)
    {
        _firmware[(type, version)] = new F7ReleaseMetadata
        {
            Version = version,
            MinCLIVersion = version,
            DownloadURL = $"https://example.org/api/v1/firmware/{type}/Meadow.OS_{version}.zip",
            NetworkDownloadURL = $"https://example.org/api/v1/firmware/{type}/Meadow.Network_{version}.zip"
        };
        return this;
    }

    public MeadowCloudClientBuilder WithFirmwareReference(string type, string version, string referencedVersion)
    {
        if (_firmware.TryGetValue((type, referencedVersion), out F7ReleaseMetadata? referencedMetadata))
        {
            _firmware[(type, version)] = referencedMetadata;
            return this;
        }

        return WithFirmware(type, version);
    }

    public MeadowCloudClientBuilder WithFirmwareResponse(string type, string version, HttpResponseMessage httpResponseMessage)
    {
        _firmwareResponses[(type, version)] = httpResponseMessage;
        return this;
    }

    public HttpClient Build()
    {
        var handler = A.Fake<FakeableHttpMessageHandler>();

        A.CallTo(() => handler
            .FakeSendAsync(A<HttpRequestMessage>.Ignored, A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(HttpStatusCode.NotFound));

        foreach (var ((type, version), metadata) in _firmware)
        {
            A.CallTo(() => handler
                .FakeSendAsync(
                    A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/firmware/{type}/{version}"),
                    A<CancellationToken>.Ignored))
                .Returns(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(metadata)
                });
        }

        foreach (var ((type, version), response) in _firmwareResponses)
        {
            A.CallTo(() => handler
                .FakeSendAsync(
                    A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/firmware/{type}/{version}"),
                    A<CancellationToken>.Ignored))
                .Returns(response);
        }

        return new HttpClient(handler) { BaseAddress = new Uri("https://example.org") };
    }
}
