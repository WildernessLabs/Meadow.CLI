using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.CloudServices;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meadow.CLI.Commands.Cloud
{
    [Command("notification send", Description = "Send a notification to message queue")]
    public class NotificationCommand : MeadowCommand
    {
        private readonly ILogger<LoginCommand> _logger;

        public NotificationCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory) : base(downloadManager, loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<LoginCommand>();
        }

        [CommandOption("message", 'm', Description = "Path to the Meadow OS binary", IsRequired = true)]
        public string[] Message { get; init; }

        [CommandOption("topic", 't', Description = "Path to the Meadow OS binary", IsRequired = true)]
        public string[] Topic { get; init; }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            NotificationService service = new NotificationService(_logger);
            var oid = await service.GetUsersOrganizationId();
            await service.SendNotification(oid, string.Join(' ', Message), string.Join(' ', Topic));
        }
    }
}
