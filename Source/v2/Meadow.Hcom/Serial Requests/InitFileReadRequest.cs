using System.Text;

namespace Meadow.Hcom;

internal class InitFileWriteRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER;

    public string LocalFileName { get; set; } = default!;
    public string MeadowFileName
    {
        get
        {
            if (Payload == null) return string.Empty;
            return Encoding.ASCII.GetString(Payload);
        }
        set
        {
            Payload = Encoding.ASCII.GetBytes(value);
        }
    }
}

internal class InitFileReadRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_UPLOAD_FILE_INIT;

    public string? LocalFileName { get; set; } = default!;
    public string MeadowFileName
    {
        get
        {
            if (Payload == null) return string.Empty;
            return Encoding.ASCII.GetString(Payload);
        }
        set
        {
            Payload = Encoding.ASCII.GetBytes(value);
        }
    }
}
