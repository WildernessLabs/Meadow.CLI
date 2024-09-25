using System.Diagnostics;
using CliFx.Infrastructure;
using Meadow.Hcom;
using Meadow.Package;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

internal static class AppTools
{
    internal static string ValidateAndSanitizeAppPath(string? path)
    {
        path ??= Directory.GetCurrentDirectory();

        path = path.Trim('\"');
        path = path.TrimEnd('\"');
        path = path.TrimEnd(Path.DirectorySeparatorChar);

        if (!File.Exists(path))
        {   // is it a valid directory?
            if (!Directory.Exists(path))
            {
                throw new CommandException($"{Strings.InvalidApplicationPath} '{path}'", CommandExitCode.FileNotFound);
            }
        }

        //sanitize path
        return Path.GetFullPath(path);
    }

    internal static async Task DisableRuntimeIfEnabled(IMeadowConnection connection, ILogger? logger, CancellationToken cancellationToken)
    {
        var isRuntimeEnabled = await connection.IsRuntimeEnabled();

        if (isRuntimeEnabled)
        {
            logger?.LogInformation($"{Strings.DisablingRuntime}...");

            await connection.RuntimeDisable(cancellationToken);
        }
    }

    internal static async Task<bool> TrimApplication(string path,
        IPackageManager packageManager,
        string osVersion,
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
            var foundConfiguration = candidates.Where(c => c.DirectoryName?.IndexOf(configuration, StringComparison.OrdinalIgnoreCase) >= 0);

            if (foundConfiguration.Count() == 0)
            {
                logger?.LogError($"{Strings.NoCompiledApplicationFound} {Strings.WithConfiguration} '{configuration}' {Strings.At} '{path}'");
                return false;
            }
            else
            {
                file = foundConfiguration
                .OrderByDescending(c => c.LastWriteTime)
                .First();

                if (file == null)
                {
                    logger?.LogError($"{Strings.NoCompiledApplicationFound} {Strings.At} '{path}'");
                    return false;
                }
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

        await packageManager.TrimApplication(file, osVersion, false, noLinkAssemblies, cancellationToken);
        cts.Cancel();

        // illink returns before all files are written - attempt a delay of 1s
        await Task.Delay(1000, cancellationToken);

        return true;
    }

    internal static string SanitizeMeadowFolderName(string fileName)
    {
        return SanitizeMeadowFilename(fileName) + '/';
    }

    internal static string SanitizeMeadowFilename(string fileName)
    {
        fileName = fileName.Replace('\\', Path.DirectorySeparatorChar);
        fileName = fileName.Replace('/', Path.DirectorySeparatorChar);

        var folder = Path.GetDirectoryName(fileName);

        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = Path.DirectorySeparatorChar + AppManager.MeadowRootFolder;
        }
        else
        {
            if (!folder.StartsWith(Path.DirectorySeparatorChar))
            {
                if (!folder.StartsWith($"{AppManager.MeadowRootFolder}"))
                {
                    folder = $"{Path.DirectorySeparatorChar}{AppManager.MeadowRootFolder}{Path.DirectorySeparatorChar}{folder}";
                }
                else
                {
                    folder = $"{Path.DirectorySeparatorChar}{folder}";
                }
            }
        }

        var meadowFileName = Path.Combine(folder, Path.GetFileName(fileName));

        return meadowFileName!.Replace(Path.DirectorySeparatorChar, '/');
    }

    internal static async Task<int> RunProcessCommand(string command, string args, Action<string>? handleOutput = null, Action<string>? handleError = null, CancellationToken cancellationToken = default)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = new Process { StartInfo = processStartInfo })
        {
            process.Start();

            var outputCompletion = ReadLinesAsync(process.StandardOutput, handleOutput, cancellationToken);
            var errorCompletion = ReadLinesAsync(process.StandardError, handleError, cancellationToken);

            await Task.WhenAll(outputCompletion, errorCompletion, process.WaitForExitAsync());

            return process.ExitCode;
        }
    }

    private static async Task ReadLinesAsync(StreamReader reader, Action<string>? handleLine, CancellationToken cancellationToken)
    {
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(line)
                && handleLine != null)
            {
                handleLine(line);
            }
        }
    }
}