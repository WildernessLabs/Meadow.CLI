namespace Meadow.Hcom;

internal class UartProfilerDisableRequest : Request
{
    public override RequestType RequestType => RequestType.HCOM_MDOW_REQUEST_NO_PROFILER_TO_UART;
}
