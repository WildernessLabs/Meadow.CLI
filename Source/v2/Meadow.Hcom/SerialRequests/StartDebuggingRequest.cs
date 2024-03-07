namespace Meadow.Hcom;

internal class StartDebuggingRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_MONO_START_DBG_SESSION;
}