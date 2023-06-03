using System.Linq;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.CloudServices;
using Meadow.CLI.Core.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Cloud.Collection;

public class ListCollectionCommand
{
    [Command("collection list", Description = "List Meadow Collections")]
    public class ListCommand : ICommand
    {
        private readonly ILogger<LogoutCommand> _logger;
        UserService _userService;
        private CollectionService _collectionService;
        IConfiguration _config;

        public ListCommand(ILoggerFactory loggerFactory, UserService userService, CollectionService collectionService,
            IConfiguration config)
        {
            _logger = loggerFactory.CreateLogger<LogoutCommand>();
            _userService = userService;
            _collectionService = collectionService;
            _config = config;
        }

        [CommandOption("orgId", 'o', Description = "Organization Id", IsRequired = false)]
        public string OrgId { get; set; }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            await Task.Yield();

            try
            {
                var userOrgs = await _userService.GetUserOrgs(cancellationToken).ConfigureAwait(false);
                if (!userOrgs.Any())
                {
                    _logger.LogInformation($"Please visit {_config["meadowCloudHost"]} to register your account.");
                    return;
                }
                else if (userOrgs.Count() > 1 && string.IsNullOrEmpty(OrgId))
                {
                    _logger.LogInformation($"Please specify the orgId.");
                    return;
                }
                else if (userOrgs.Count() == 1 && string.IsNullOrEmpty(OrgId))
                {
                    OrgId = userOrgs.First().Id;
                }

                if (!userOrgs.Select(x => x.Id).Contains(OrgId))
                {
                    _logger.LogInformation($"Invalid orgId: {OrgId}");
                    return;
                }
            }
            catch (MeadowCloudAuthException)
            {
                _logger.LogInformation($"You must be signed in to execute this command.");
                return;
            }

            var collections = await _collectionService.GetOrgCollections(OrgId, cancellationToken);

            if (collections == null || collections.Count() == 0)
            {
                _logger.LogInformation("No collections found.");
            }

            foreach (var collection in collections)
            {
                _logger.LogInformation($"{collection.Id} | {collection.Name}");
            }
        }
    }
}