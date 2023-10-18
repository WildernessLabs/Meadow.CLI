using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("developer", Description = "Sets a specified developer parameter on the Meadow")]
public class DeveloperCommand : BaseDeviceCommand<DeveloperCommand>
{
    [CommandOption("param", 'p', Description = "The parameter to set.")]
    public ushort Parameter { get; set; }

    [CommandOption("value", 'v', Description = "The value to apply to the parameter. Valid values are 0 to 4,294,967,295")]
    public uint Value { get; set; }

    public DeveloperCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

        if (Connection != null)
        {
            if (Connection.Device != null)
            {
                Logger?.LogInformation($"Setting developer parameter {Parameter} to {Value}");
                await Connection.Device.SetDeveloperParameter(Parameter, Value, CancellationToken);
            }
        }
    }
}