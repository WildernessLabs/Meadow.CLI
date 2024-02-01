namespace Meadow.Hcom
{
    internal class DebuggerDataRequest : Request
    {
        public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_DEBUGGING_DEBUGGER_DATA;

        public byte[] DebuggerData
        {
            get
            {
                if (Payload == null) return new byte[0];
                return Payload;
            }
            set
            {
                Payload = value;
            }
        }
    }
}