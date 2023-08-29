using CliFx.Attributes;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("port list", Description = "List available local serial ports")]
public class PortListCommand : BaseDeviceCommand<PortListCommand>
{
    public PortListCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand(IMeadowConnection connection, Hcom.IMeadowDevice device, CancellationToken cancellationToken)
    {
        foreach (var port in await MeadowConnectionManager.GetSerialPorts())
        {
            Logger.LogInformation("Found Meadow: {port}", port);
        }
    }
}
