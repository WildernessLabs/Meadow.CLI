namespace Meadow.Hcom;

internal class NshEnableDisableRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH;
}