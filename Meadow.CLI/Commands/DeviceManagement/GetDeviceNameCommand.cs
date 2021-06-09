using System;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement
{
    [Command("device name", Description = "Get the name of the Meadow")]
    public class GetDeviceNameCommand : MeadowSerialCommand
    {
        private readonly ILogger<GetDeviceNameCommand> _logger;

        public GetDeviceNameCommand(ILoggerFactory loggerFactory,
                                    MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<GetDeviceNameCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            var deviceName = await Meadow.GetDeviceNameAsync(TimeSpan.FromSeconds(5), cancellationToken: cancellationToken)
                                         .ConfigureAwait(false);

            _logger.LogInformation($"Device Name: {deviceName}");
        }
    }
}