namespace Meadow.Hcom;

internal class GetRtcTimeRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_RTC_READ_TIME_CMD;
}
