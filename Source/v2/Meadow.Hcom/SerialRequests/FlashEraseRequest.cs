namespace Meadow.Hcom;

internal class FlashEraseRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_BULK_FLASH_ERASE;
}
