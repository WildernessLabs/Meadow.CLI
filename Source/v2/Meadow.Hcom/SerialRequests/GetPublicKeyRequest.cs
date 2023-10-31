namespace Meadow.Hcom;

internal class GetPublicKeyRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_OTA_REGISTER_DEVICE;
}
