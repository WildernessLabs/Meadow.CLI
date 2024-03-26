namespace Meadow.Hcom;

internal class EndFileWriteRequest : Request
{
    private RequestType _requestType = RequestType.HCOM_MDOW_REQUEST_END_FILE_TRANSFER;

    public override RequestType RequestType => _requestType;

    public void SetRequestType(RequestType requestType)
    {
        _requestType = requestType;
    }
}
