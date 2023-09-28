using System.Text;

namespace Meadow.Hcom;

internal class FileDeleteRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_DELETE_FILE_BY_NAME;

    public string MeadowFileName
    {
        get
        {
            if (Payload == null) return string.Empty;
            return Encoding.ASCII.GetString(Payload, 44, Payload.Length - 44);
        }
        set
        {
            var nameBytes = Encoding.ASCII.GetBytes(value);
            Payload = new byte[4 + 4 + 4 + 32 + nameBytes.Length];
            Array.Copy(nameBytes, 0, Payload, 44, nameBytes.Length); // file name
        }
    }
}
