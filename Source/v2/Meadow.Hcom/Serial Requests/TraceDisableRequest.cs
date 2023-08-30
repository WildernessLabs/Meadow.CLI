namespace Meadow.Hcom;

internal class TraceDisableRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_NO_TRACE_TO_HOST;
}
