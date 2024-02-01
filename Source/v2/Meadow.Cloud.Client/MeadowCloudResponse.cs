using System.Net;

namespace Meadow.Cloud.Client;

public class MeadowCloudResponse
{
    public MeadowCloudResponse(HttpStatusCode statusCode, IReadOnlyDictionary<string, IEnumerable<string>> headers)
    {
        StatusCode = statusCode;
        Headers = headers;
    }

    public HttpStatusCode StatusCode { get; }

    public IReadOnlyDictionary<string, IEnumerable<string>> Headers { get; }
}

public class MeadowCloudResponse<TResult> : MeadowCloudResponse
{
    public MeadowCloudResponse(HttpStatusCode statusCode, IReadOnlyDictionary<string, IEnumerable<string>> headers, TResult result)
        : base(statusCode, headers)
    {
        Result = result;

    }

    public TResult Result { get; private set; }
}
