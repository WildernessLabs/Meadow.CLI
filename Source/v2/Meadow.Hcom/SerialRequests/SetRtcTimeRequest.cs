using System.Text;

namespace Meadow.Hcom;

internal class SetRtcTimeRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_RTC_SET_TIME_CMD;

    public DateTimeOffset? Time
    {
        get
        {
            if (Payload?.Length == 0)
                return null;

            return DateTimeOffset.Parse(Encoding.ASCII.GetString(Payload));
        }
        set
        {
            if (value.HasValue)
            {
                base.Payload = Encoding.ASCII.GetBytes(value.Value.ToUniversalTime().ToString("o"));
            }
        }
    }

}
