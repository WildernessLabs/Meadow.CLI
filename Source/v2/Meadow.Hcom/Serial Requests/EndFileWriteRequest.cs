namespace Meadow.Hcom;

internal class EndFileWriteRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_END_FILE_TRANSFER;
}
