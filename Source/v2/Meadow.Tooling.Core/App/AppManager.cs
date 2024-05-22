using Meadow.Hcom;
using Meadow.Linker;
using Meadow.Package;
using Meadow.Software;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI;

public static class AppManager
{
    static readonly string MeadowRootFolder = "meadow0";

    static readonly string[] PersistantFolders = new string[]
    {
        "Data",
        "Documents",
        "update-store",
    };

    private static bool MatchingDllExists(string file)
    {
        return File.Exists(Path.ChangeExtension(file, ".dll"));
    }

    private static bool IsPdb(string file)
    {
        return string.Compare(Path.GetExtension(file), ".pdb", StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static bool IsXmlDoc(string file)
    {
        if (string.Compare(Path.GetExtension(file), ".xml", StringComparison.OrdinalIgnoreCase) == 0)
        {
            return MatchingDllExists(file);
        }
        return false;
    }

    public static async Task DeployApplication(
        IPackageManager packageManager,
        IMeadowConnection connection,
        string osVersion,
        string localBinaryDirectory,
        bool includePdbs,
        bool includeXmlDocs,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        // TODO: add sub-folder support when HCOM supports it
        var localFiles = new Dictionary<string, uint>();

        var dependencies = new List<string>();

        var processedAppPath = localBinaryDirectory;

        //check if there's a post link folder
        if (Directory.Exists(Path.Combine(localBinaryDirectory, MeadowLinker.PostLinkDirectoryName)))
        {
            logger?.LogInformation($"Found trimmed binaries in post link folder...");

            processedAppPath = Path.Combine(localBinaryDirectory, MeadowLinker.PostLinkDirectoryName);

            //add all dlls from the postlink_bin folder to the dependencies
            dependencies = Directory.EnumerateFiles(processedAppPath, "*.dll", SearchOption.TopDirectoryOnly).ToList();
            dependencies.Remove(Path.Combine(processedAppPath, "App.dll"));

            //add all pdbs from the postlink_bin folder to the dependencies if includePdbs is true
            if (includePdbs)
            {
                dependencies.AddRange(Directory.EnumerateFiles(processedAppPath, "*.pdb", SearchOption.TopDirectoryOnly));
                dependencies.Remove(Path.Combine(processedAppPath, "App.pdb"));
            }
        }
        else
        {
            logger?.LogInformation($"Did not find trimmed binaries folder...");

            dependencies = packageManager.GetDependencies(new FileInfo(Path.Combine(processedAppPath, "App.dll")), osVersion);
        }
        dependencies.Add(Path.Combine(localBinaryDirectory, "App.dll"));

        if (includePdbs)
        {
            dependencies.Add(Path.Combine(localBinaryDirectory, "App.pdb"));
        }

        var binaries = Directory.EnumerateFiles(localBinaryDirectory, "*.*", SearchOption.AllDirectories)
            .Where(s => new FileInfo(s).Extension != ".dll")
            .Where(s => new FileInfo(s).Extension != ".pdb")
            .Where(s => !s.Contains(".DS_Store")).ToList();
        dependencies.AddRange(binaries);

        logger?.LogInformation("Generating list of files to deploy...");

        foreach (var file in dependencies)
        {
            // Add any other filtering capability here
            if (!includePdbs && IsPdb(file)) { continue; }
            if (!includeXmlDocs && IsXmlDoc(file)) { continue; }

            // read the file data so we can generate a CRC
            using FileStream fs = File.Open(file, FileMode.Open);
            var len = (int)fs.Length;
            var bytes = new byte[len];

            await fs.ReadAsync(bytes, 0, len, cancellationToken);

            var crc = CrcTools.Crc32part(bytes, len, 0);

            localFiles.Add(file, crc);
        }

        if (localFiles.Count == 0)
        {
            logger?.LogInformation($"No new files to deploy");
        }

        // get a list of files on-device, with CRCs
        var deviceFiles = await GetFilesInFolder(connection, $"/{MeadowRootFolder}/", cancellationToken);

        // get a list of MeadowFileInfo of the device files that are not in the list we intend to deploy
        var removeFiles = deviceFiles
            .Where(f => !localFiles.Keys.Select(f => Path.GetFileName(f)).Contains(Path.GetFileName(f.Name)))
            .ToList();

        // delete those files
        foreach (var file in removeFiles)
        {
            logger?.LogInformation($"Deleting file '{file}'...");
            var folder = string.IsNullOrEmpty(file.Path) ? $"/{MeadowRootFolder}/" : $"{file.Path}";

            await connection.DeleteFile($"{folder}{file.Name}", cancellationToken);
        }

        // now send all files with differing CRCs
        foreach (var localFile in localFiles)
        {
            string? meadowFilename = string.Empty;
            if (localFile.Key.Contains(PackageManager.PreLinkDirectoryName) ||
                localFile.Key.Contains(PackageManager.PackageOutputDirectoryName))
            {
                continue;
            }
            else if (localFile.Key.Contains(PackageManager.PostLinkDirectoryName))
            {   //we want to transfer the file but we can let the API find the file name
                meadowFilename = null;
            }
            else
            {   //may have a sub folder so we manually process the file name + path
                meadowFilename = GetTargetMeadowFileName(localBinaryDirectory, localFile.Key);
            }
            var existing = deviceFiles.FirstOrDefault(f => Path.GetFileName(f.Name) == Path.GetFileName(localFile.Key));

            if (existing != null && existing.Crc != null)
            {
                var crc = uint.Parse(existing.Crc.Substring(2), System.Globalization.NumberStyles.HexNumber);

                if (crc == localFile.Value)
                {   // exists and has a matching CRC, skip it
                    logger?.LogInformation($"Skipping '{localFile.Key}'");
                    continue;
                }
            }

            logger?.LogInformation($"Sending  '{localFile.Key}'");
        send_file:

            if (!await connection.WriteFile(localFile.Key, meadowFilename, cancellationToken))
            {
                logger?.LogWarning($"Error sending'{Path.GetFileName(localFile.Key)}' - retrying");
                await Task.Delay(100);
                goto send_file;
            }
        }

        //on macOS, if we don't write a blank line we lose the writing notifcation for the last file
        logger?.LogInformation(string.Empty);
    }

    static async Task<List<MeadowFileInfo>> GetFilesInFolder(IMeadowConnection connection, string folder, CancellationToken? cancellationToken)
    {
        var deviceFiles = new List<MeadowFileInfo>();

        var rootFiles = await connection.GetFileList(folder, true, cancellationToken) ?? Array.Empty<MeadowFileInfo>();

        foreach (var file in rootFiles)
        {
            if (file.IsDirectory)
            {
                if (PersistantFolders.Contains(file.Name))
                {
                    continue;
                }

                //call recursively 
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

    static string GetTargetMeadowFileName(string localBinaryFolder, string fullyQualifiedFilePath)
    {
        string relativePath = string.Empty;
        string fileName = Path.GetFileName(fullyQualifiedFilePath);
        string? filePath = Path.GetDirectoryName(fullyQualifiedFilePath);

        if (filePath is not null && filePath.StartsWith(localBinaryFolder))
        {
            relativePath = filePath.Substring(localBinaryFolder.Length);

            relativePath = relativePath.Replace("\\", "/");

            //remove leading slash
            if (relativePath.StartsWith(Path.DirectorySeparatorChar.ToString()) ||
                relativePath.StartsWith("/") ||
                relativePath.StartsWith("\\"))
            {
                relativePath = relativePath.Substring(1);
            }

            if (!string.IsNullOrWhiteSpace(relativePath))
            {
                //add trailing slash
                relativePath += "/";
            }
        }

        return $"/{MeadowRootFolder}/" + relativePath + fileName;
    }
}