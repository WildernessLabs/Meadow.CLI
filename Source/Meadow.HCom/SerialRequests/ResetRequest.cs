namespace Meadow.Hcom;

internal class ResetRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_RESTART_PRIMARY_MCU;
}
