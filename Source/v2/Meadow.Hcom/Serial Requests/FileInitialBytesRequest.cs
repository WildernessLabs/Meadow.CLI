using System.Text;

namespace Meadow.Hcom;

internal class FileInitialBytesRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_GET_INITIAL_FILE_BYTES;

    public string MeadowFileName
    {
        get
        {
            if (Payload == null) return string.Empty;
            return Encoding.UTF8.GetString(Payload);
        }
        set
        {
            Payload = Encoding.UTF8.GetBytes(value);
        }
    }
}
