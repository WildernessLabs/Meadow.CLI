using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Meadow.Hcom;
using Meadow.Package;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI;

public static class AppTools
{
    public static string ValidateAndSanitizeAppPath(string? path)
    {
        path ??= Directory.GetCurrentDirectory();

        path = path.Trim('\"');
        path = path.TrimEnd('\"');
        path = path.TrimEnd(Path.DirectorySeparatorChar);

        if (!File.Exists(path))
        {   // is it a valid directory?
            if (!Directory.Exists(path))
            {
                throw new CommandException($"{Strings.InvalidApplicationPath} '{path}'", (int)CommandExitCode.FileNotFound);
            }
        }

        //sanitize path
        return Path.GetFullPath(path);
    }

    public static async Task DisableRuntimeIfEnabled(IMeadowConnection connection, ILogger? logger, CancellationToken cancellationToken)
    {
        var isRuntimeEnabled = await connection.IsRuntimeEnabled();

        if (isRuntimeEnabled)
        {
            logger?.LogInformation($"{Strings.DisablingRuntime}...");

            await connection.RuntimeDisable(cancellationToken);
        }
    }

    public static async Task EnableRuntimeIfDisabled(IMeadowConnection connection, ILogger? logger, CancellationToken cancellationToken)
    {
        var isRuntimeEnabled = await connection.IsRuntimeEnabled();

        if (!isRuntimeEnabled)
        {
            logger?.LogInformation($"{Strings.EnablingRuntime}...");

            await connection.RuntimeEnable(cancellationToken);
        }
    }

    public static async Task<bool> TrimApplication(string path,
        IPackageManager packageManager,
        string? configuration,
        IEnumerable<string>? noLinkAssemblies,
        ILogger? logger,
        IConsole? console,
        CancellationToken cancellationToken)
    {
        // it's a directory - we need to determine the latest build (they might have a Debug and a Release config)
        var candidates = PackageManager.GetAvailableBuiltConfigurations(path, "App.dll");

        if (candidates.Length == 0)
        {
            logger?.LogError($"{Strings.NoCompiledApplicationFound} at '{path}'");
            return false;
        }

        FileInfo? file;

        if (configuration is not null)
        {
            file = candidates.Where(c => c.DirectoryName.Contains(configuration)).OrderByDescending(c => c.LastWriteTime).First();

            if (file == null)
            {
                logger?.LogError($"{Strings.NoCompiledApplicationFound} at '{path}'");
                return false;
            }
        }
        else
        {
            file = candidates.OrderByDescending(c => c.LastWriteTime).First();
        }

        var cts = new CancellationTokenSource();

        if (console is not null)
        {
            ConsoleSpinner.Spin(console, cancellationToken: cts.Token);
        }

        logger?.LogInformation($"Trimming application {file.FullName}...");
        if (noLinkAssemblies != null && noLinkAssemblies.Count() > 0)
        {
            logger?.LogInformation($"Skippping assemblies: {string.Join(", ", noLinkAssemblies)}");
        }

        await packageManager.TrimApplication(file, false, noLinkAssemblies, cancellationToken);
        cts.Cancel();

        // illink returns before all files are written - attempt a delay of 1s
        await Task.Delay(1000, cancellationToken);

        return true;
    }
}