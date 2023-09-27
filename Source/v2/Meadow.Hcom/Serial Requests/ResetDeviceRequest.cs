namespace Meadow.Hcom
{
    internal class ResetDeviceRequest : Request
    {
        public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_RESTART_PRIMARY_MCU;

        public ResetDeviceRequest()
        {
        }
        // Serialized example:
        // message
        // 01-00-07-00-12-01-00-00-00-00-00-00"
        // encoded
        // 00-02-2A-02-06-03-12-01-01-01-01-01-01-01-00
    }
}