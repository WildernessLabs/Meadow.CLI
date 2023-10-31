namespace Meadow.Hcom;

internal class TraceEnableRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_SEND_TRACE_TO_HOST;
}
