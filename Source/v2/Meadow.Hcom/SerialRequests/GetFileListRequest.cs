namespace Meadow.Hcom
{
    internal class GetFileListRequest : Request
    {
        public override RequestType RequestType => IncludeCrcs
                ? RequestType.HCOM_MDOW_REQUEST_LIST_FILES_SUBDIR_CRC
                : RequestType.HCOM_MDOW_REQUEST_LIST_FILES_SUBDIR;

        public bool IncludeCrcs { get; set; }

        public string? Path
        {
            get
            {
                if (Payload == null) return null;

                if (Payload.Length == 0) { return null; }

                return Encoding.ASCII.GetString(Payload).Trim();
            }
            set
            {
                if (value != null)
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