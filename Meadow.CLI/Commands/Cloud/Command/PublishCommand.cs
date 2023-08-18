using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Meadow.CLI.Core.CloudServices;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading.Tasks;

namespace Meadow.CLI.Commands.Cloud.Command
{
    public enum QualityOfService
    {
        AtLeastOnce = 0,
        AtMostOnce = 1,
        ExactlyOnce = 2
    }

    [Command("cloud command publish", Description = "Publish a command to Meadow devices via the Meadow Service")]
    public class PublishCommand : ICommand
    {
        private readonly ILogger<PublishCommand> _logger;
        private readonly CommandService _commandService;
        private readonly IdentityManager _identityManager;
        IConfiguration _config;

        public PublishCommand(ILoggerFactory loggerFactory, CommandService commandService, IdentityManager identityManager, IConfiguration config)
        {
            _logger = loggerFactory.CreateLogger<PublishCommand>();
            _commandService = commandService;
            _identityManager = identityManager;
            _config = config;
        }

        [CommandParameter(0, Description = "The name of the command", IsRequired = true, Name = "COMMAND_NAME")]
        public string CommandName { get; set; }

        [CommandOption("collectionId", 'c', Description = "The target collection for publishing the command", IsRequired = true)]
        public string CollectionId { get; set; }

        [CommandOption("args", 'a', Description = "The arguments for the command as a JSON string", Converter = typeof(JsonDocumentBindingConverter))]
        public JsonDocument Arguments { get; set; }

        [CommandOption("qos", 'q', Description = "The MQTT-defined quality of service for the command")]
        public QualityOfService QualityOfService { get; set; }

        [CommandOption("host", Description = "Optionally set a host (default is https://www.meadowcloud.co)")]
        public string Host { get; set; }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            await Task.Yield();

            var token = await _identityManager.GetAccessToken(cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new CommandException("You must be signed into Meadow.Cloud to execute this command. Run 'meadow cloud login' to do so.");
            }

            try
            {
                _logger.LogInformation($"Publishing '{CommandName}' command to Meadow.Cloud. Please wait...");
                await _commandService.PublishCommandForCollection(CollectionId, CommandName, Arguments, (int)QualityOfService, Host, cancellationToken);
                _logger.LogInformation("Publish command successful.");
            }
            catch (MeadowCloudAuthException ex)
            {
                throw new CommandException("You must be signed in to execute this command.", innerException: ex);
            }
            catch (MeadowCloudException ex)
            {
                throw new CommandException($"Publish command failed: {ex.Message}", innerException: ex);
            }
        }
    }
}
