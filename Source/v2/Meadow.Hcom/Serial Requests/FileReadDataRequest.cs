namespace Meadow.Hcom;

internal class FileReadDataRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_HOST_REQUEST_UPLOADING_FILE_DATA;
}
