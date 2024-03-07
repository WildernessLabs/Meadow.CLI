namespace Meadow.Cloud.Client;

public class MeadowCloudResponse
{
    public MeadowCloudResponse(HttpStatusCode statusCode, IReadOnlyDictionary<string, IEnumerable<string>> headers)
    {
        StatusCode = statusCode;
        Headers = headers;
    }

    public MeadowCloudResponse(HttpStatusCode statusCode) : this(statusCode, new Dictionary<string, IEnumerable<string>>()) { }

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

    public MeadowCloudResponse(HttpStatusCode statusCode, TResult result) : this(statusCode, new Dictionary<string, IEnumerable<string>>(), result) { }

    public TResult Result { get; }
}
