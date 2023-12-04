using System.Text;

namespace Meadow.Hcom
{
    internal class GetFileListRequest : Request
    {
        public override RequestType RequestType => IncludeCrcs
                ? RequestType.HCOM_MDOW_REQUEST_LIST_PART_FILES_AND_CRC
                : RequestType.HCOM_MDOW_REQUEST_LIST_PARTITION_FILES;

        public bool IncludeCrcs { get; set; }

        public string? Path
        {
            get
            {
                if (Payload?.Length == 0)
                    return null;

                return Encoding.ASCII.GetString(Payload);
            }
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    base.Payload = Encoding.ASCII.GetBytes(value);
                }
            }
        }

        public GetFileListRequest()
        {
        }
    }
}