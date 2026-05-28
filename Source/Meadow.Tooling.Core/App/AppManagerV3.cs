using Meadow.Hcom;
using Meadow.Software;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI;

/// <summary>
/// Deployment manager for Meadow OS 3.x+.
/// Uses .NET's built-in publish trimming (System.Private.CoreLib, no custom ILLink).
/// All files in the build output directory are deployed directly.
/// </summary>
public static class AppManagerV3
{
    public static async Task DeployApplication(
        IMeadowConnection connection,
        string localBinaryDirectory,
        bool includePdbs,
        bool includeXmlDocs,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var localFiles = new Dictionary<string, uint>();

        logger?.LogInformation("Generating list of files to deploy (Meadow v3)...");

        var runtimesDir = Path.Combine(localBinaryDirectory, "runtimes") + Path.DirectorySeparatorChar;
        var files = Directory.EnumerateFiles(localBinaryDirectory, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(runtimesDir, StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains(".DS_Store"))
            .Where(f => IsDeployableFile(f))
            .Where(f => !IsPdb(f) || includePdbs)
            .Where(f => !IsXmlDoc(f) || includeXmlDocs);

        foreach (var file in files)
        {
            using FileStream fs = File.Open(file, FileMode.Open);
            var len = (int)fs.Length;
            var bytes = new byte[len];
            await fs.ReadAsync(bytes, 0, len, cancellationToken);

            var crc = CrcTools.Crc32part(bytes, len, 0);
            localFiles.Add(file, crc);
        }

        if (localFiles.Count == 0)
        {
            logger?.LogInformation("No files to deploy");
            return;
        }

        // get device file list with CRCs
        var deviceFiles = await GetFilesInFolder(connection, $"/{AppManager.MeadowRootFolder}/", cancellationToken);

        // delete files on device that aren't in the new deployment
        var removeFiles = deviceFiles
            .Where(f => !localFiles.Keys.Select(k => Path.GetFileName(k)).Contains(Path.GetFileName(f.Name)))
            .ToList();

        foreach (var file in removeFiles)
        {
            if (AppManager.PersistantFolders.Contains(file.Path))
            {
                continue;
            }

            logger?.LogInformation($"Deleting '{file}'".PadRight(80));
            var folder = NormalizeDeviceFolder(file.Path);
            await connection.DeleteFile($"{folder}{file.Name}", cancellationToken);
        }

        // send files with differing CRCs
        foreach (var localFile in localFiles)
        {
            var meadowFilename = GetTargetMeadowFileName(localBinaryDirectory, localFile.Key);

            var existing = deviceFiles.FirstOrDefault(f => Path.GetFileName(f.Name) == Path.GetFileName(localFile.Key));

            if (existing != null && existing.Crc != null)
            {
                var crc = uint.Parse(existing.Crc.Substring(2), System.Globalization.NumberStyles.HexNumber);
                if (crc == localFile.Value)
                {
                    logger?.LogInformation($"Skipping '{Path.GetFileName(localFile.Key)}'".PadRight(80));
                    continue;
                }
            }

            logger?.LogInformation($"Sending  '{Path.GetFileName(localFile.Key)}'".PadRight(80));

        send_file:
            if (!await connection.WriteFile(localFile.Key, meadowFilename, cancellationToken))
            {
                logger?.LogWarning($"Error sending '{Path.GetFileName(localFile.Key)}' - retrying");
                await Task.Delay(100);
                goto send_file;
            }
        }

        logger?.LogInformation(string.Empty.PadRight(80));
    }

    private static async Task<List<MeadowFileInfo>> GetFilesInFolder(IMeadowConnection connection, string folder, CancellationToken? cancellationToken)
    {
        var deviceFiles = new List<MeadowFileInfo>();
        var rootFiles = await connection.GetFileList(folder, true, cancellationToken) ?? Array.Empty<MeadowFileInfo>();

        foreach (var file in rootFiles)
        {
            if (file.IsDirectory)
            {
                if (AppManager.PersistantFolders.Contains(file.Name))
                {
                    continue;
                }

                var subfolderFiles = await GetFilesInFolder(connection, file.Name, cancellationToken);
                if (subfolderFiles != null)
                {
                    deviceFiles.AddRange(subfolderFiles);
                }
            }
            else
            {
                deviceFiles.Add(file);
            }
        }

        return deviceFiles;
    }

    private static string NormalizeDeviceFolder(string? path)
    {
        var folder = string.IsNullOrEmpty(path) ? $"/{AppManager.MeadowRootFolder}/" : path;

        if (!folder.StartsWith("/"))
            folder = "/" + folder;
        if (!folder.EndsWith("/"))
            folder += "/";
        if (!folder.Contains(AppManager.MeadowRootFolder))
            folder = $"/{AppManager.MeadowRootFolder}{folder}";

        return folder;
    }

    private static string GetTargetMeadowFileName(string localBinaryFolder, string fullyQualifiedFilePath)
    {
        string fileName = Path.GetFileName(fullyQualifiedFilePath);
        string? filePath = Path.GetDirectoryName(fullyQualifiedFilePath);
        string relativePath = string.Empty;

        if (filePath is not null && filePath.StartsWith(localBinaryFolder))
        {
            relativePath = filePath.Substring(localBinaryFolder.Length)
                .Replace("\\", "/")
                .TrimStart('/', '\\');

            if (!string.IsNullOrWhiteSpace(relativePath))
            {
                relativePath += "/";
            }
        }

        return $"/{AppManager.MeadowRootFolder}/" + relativePath + fileName;
    }

    // Only deploy managed assemblies and config files. Self-contained publish includes
    // native runtime files (libcoreclr.so, apphost, etc.) that Meadow doesn't need.
    private static readonly HashSet<string> DeployableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll", ".pdb", ".json", ".xml", ".yaml", ".yml", ".config",
    };

    private static bool IsDeployableFile(string file)
    {
        var ext = Path.GetExtension(file);
        return !string.IsNullOrEmpty(ext) && DeployableExtensions.Contains(ext);
    }

    private static bool IsPdb(string file)
    {
        return string.Compare(Path.GetExtension(file), ".pdb", StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static bool IsXmlDoc(string file)
    {
        if (string.Compare(Path.GetExtension(file), ".xml", StringComparison.OrdinalIgnoreCase) == 0)
        {
            return File.Exists(Path.ChangeExtension(file, ".dll"));
        }
        return false;
    }
}
