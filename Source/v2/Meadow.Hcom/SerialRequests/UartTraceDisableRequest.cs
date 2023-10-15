namespace Meadow.Hcom;

internal class UartTraceDisableRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_NO_TRACE_TO_UART;
}
