namespace Meadow.Cloud.Client;

public class MeadowCloudResponse(HttpStatusCode statusCode, IReadOnlyDictionary<string, IEnumerable<string>> headers)
{
    public MeadowCloudResponse(HttpStatusCode statusCode) : this(statusCode, new Dictionary<string, IEnumerable<string>>()) { }

    public HttpStatusCode StatusCode { get; } = statusCode;
    public IReadOnlyDictionary<string, IEnumerable<string>> Headers { get; } = headers;
}

public class MeadowCloudResponse<TResult>(HttpStatusCode statusCode, IReadOnlyDictionary<string, IEnumerable<string>> headers, TResult result)
    : MeadowCloudResponse(statusCode, headers)
{
    public MeadowCloudResponse(HttpStatusCode statusCode, TResult result) : this(statusCode, new Dictionary<string, IEnumerable<string>>(), result) { }

    public TResult Result { get; } = result;
}
