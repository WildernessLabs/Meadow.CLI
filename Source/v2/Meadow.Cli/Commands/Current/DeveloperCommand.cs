using CliFx.Attributes;
using Meadow.Hcom;
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
        Logger?.LogInformation($"Setting developer parameter {Parameter} to {Value}");

        if (CurrentConnection != null)
        {
            CurrentConnection.DeviceMessageReceived += (s, e) =>
            {
                Logger?.LogInformation(e.message);
            };
            CurrentConnection.ConnectionError += (s, e) =>
            {
                Logger?.LogError(e.Message);
            };

            await CurrentConnection.Device.SetDeveloperParameter(Parameter, Value, CancellationToken);
        }
    }
}

