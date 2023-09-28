using System;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.Hardware;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("port list", Description = "List available local serial ports")]
public class PortListCommand : BaseCommand<PortListCommand>
{
    public IList<string>? Portlist;

    public PortListCommand(ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand(CancellationToken? cancellationToken)
    {
        Portlist = await MeadowConnectionManager.GetSerialPorts();
        if (Portlist.Count > 0)
        {
            var plural = Portlist.Count > 1 ? "s" : string.Empty;
            Logger?.LogInformation($"Found the following device{plural} -");
            for (int i = 0; i < Portlist.Count; i++)
            {
                Logger?.LogInformation($"{i + 1}: {Portlist[i]}");
            }
        }
    }
}