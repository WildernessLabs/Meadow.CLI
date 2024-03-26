namespace Meadow.Hcom;

internal class GetRuntimeStateRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_MONO_RUN_STATE;
}
