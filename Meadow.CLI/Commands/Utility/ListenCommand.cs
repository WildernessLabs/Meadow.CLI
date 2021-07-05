using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Internals.MeadowCommunication;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Utility
{
    [Command("listen", Description = "Listen for console output from Meadow")]
    public class ListenCommand : MeadowSerialCommand
    {
        private readonly ILogger<InstallDfuUtilCommand> _logger;

        public ListenCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = loggerFactory.CreateLogger<InstallDfuUtilCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();
            
            _logger.LogInformation("Listening for Meadow Console output. Press Ctrl+C to exit");
            void ResponseHandler(object s, MeadowMessageEventArgs e)
            {
                if (e.MessageType == MeadowMessageType.Data)
                    _logger.LogInformation(e.Message);
            };
            Meadow.MeadowDevice.DataProcessor.OnReceiveData += ResponseHandler;
            try
            {
                await Task.Delay(-1, cancellationToken)
                          .ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
            finally
            {
                Meadow.MeadowDevice.DataProcessor.OnReceiveData -= ResponseHandler;
            }
        }
    }
}