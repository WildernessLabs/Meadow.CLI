namespace Meadow.Hcom;

internal class TraceLevelRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_CHANGE_TRACE_LEVEL;
}
