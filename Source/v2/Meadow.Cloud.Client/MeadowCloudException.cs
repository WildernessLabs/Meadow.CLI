namespace Meadow.Cloud.Client;

public class MeadowCloudException : Exception
{
    public MeadowCloudException(string message, HttpStatusCode statusCode, string? response, IReadOnlyDictionary<string, IEnumerable<string>> headers, Exception? innerException)
        : base(message + "\r\n\r\nStatus: " + statusCode + "\r\nResponse: \r\n" + ((response == null) ? "(null)" : response.Substring(0, response.Length >= 512 ? 512 : response.Length)), innerException)
    {
        StatusCode = statusCode;
        Response = response;
        Headers = headers;
    }

    public HttpStatusCode StatusCode { get; private set; }

    public string? Response { get; private set; }

    public IReadOnlyDictionary<string, IEnumerable<string>> Headers { get; private set; }

    public override string ToString()
    {
        return string.Format("HTTP Response: \n\n{0}\n\n{1}", Response, base.ToString());
    }
}

public class MeadowCloudException<TResult> : MeadowCloudException
{
    public MeadowCloudException(string message, HttpStatusCode statusCode, string? response, IReadOnlyDictionary<string, IEnumerable<string>> headers, TResult result, Exception? innerException)
            : base(message, statusCode, response, headers, innerException)
    {
        Result = result;
    }

    public TResult Result { get; private set; }
}


public class MeadowCloudClassicException : Exception
{
    public MeadowCloudClassicException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}