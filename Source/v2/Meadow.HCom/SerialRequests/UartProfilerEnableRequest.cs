namespace Meadow.Hcom;

internal class UartProfilerEnableRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_SEND_PROFILER_TO_UART;
}
