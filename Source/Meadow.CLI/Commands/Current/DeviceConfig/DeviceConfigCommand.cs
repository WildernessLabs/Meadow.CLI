using Microsoft.Extensions.Logging;
using System.Collections;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class DeviceConfigCommand<T> : BaseDeviceCommand<T>
{
    public DeviceConfigCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected void ShowSettings(ILogger? logger, IDictionary dict, int level = 0, List<bool> isLastLevel = null)
    {
        isLastLevel ??= new List<bool>();
        var keys = dict.Keys.Cast<object>().ToList();

        for (int i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var isLast = i == keys.Count - 1;
            var indent = string.Join("", isLastLevel.Select(last => last ? "   " : "│  "));
            var prefix = isLast ? "└─" : "├─";

            if (dict[key] is IDictionary childDict)
            {
                logger?.LogInformation($"{indent}{prefix} {key}");
                isLastLevel.Add(isLast);
                ShowSettings(logger, childDict, level + 1, isLastLevel);
                isLastLevel.RemoveAt(isLastLevel.Count - 1);
            }
            else
            {
                logger?.LogInformation($"{indent}{prefix} {key}={dict[key]}");
            }
        }
    }
}
