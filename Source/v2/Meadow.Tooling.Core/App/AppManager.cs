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
        IMeadowConnection meadowConnection,
        string localBinaryDirectory,
        bool includePdbs,
        bool includeXmlDocs,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        bool isRuntimeEnabled;

        try
        {
            // in order to deploy, the runtime must be disabled
            isRuntimeEnabled = await meadowConnection.IsRuntimeEnabled();

            if (isRuntimeEnabled)
            {
                logger?.LogInformation($"Disabling runtime...");

                await meadowConnection.RuntimeDisable(cancellationToken);
            }

            // TODO: add sub-folder support when HCOM supports it
            var localFiles = new Dictionary<string, uint>();

            var dependencies = new List<string>();

            var processedAppPath = localBinaryDirectory;

            var postLinkDirectoryPath = Path.Combine(localBinaryDirectory, MeadowLinker.PostLinkDirectoryName);

            //check if there's a post link folder
            if (Directory.Exists(postLinkDirectoryPath))
            {
                processedAppPath = postLinkDirectoryPath;

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
                dependencies = packageManager.GetDependencies(new FileInfo(Path.Combine(processedAppPath, "App.dll")));
            }
            dependencies.Add(Path.Combine(localBinaryDirectory, "App.dll"));

            if (includePdbs)
            {
                dependencies.Add(Path.Combine(localBinaryDirectory, "App.pdb"));
            }

            var binaries = Directory.EnumerateFiles(localBinaryDirectory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(s => new FileInfo(s).Extension != ".dll")
                .Where(s => new FileInfo(s).Extension != ".pdb")
                .Where(s => !s.Contains(".DS_Store")).ToList();
            dependencies.AddRange(binaries);

            logger?.LogInformation("Generating list of files to deploy...");

            foreach (var file in dependencies)
            {
                // TODO: add any other filtering capability here
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
            var deviceFiles = await meadowConnection.GetFileList("/meadow0/", true, cancellationToken) ?? Array.Empty<MeadowFileInfo>();

            // get a list of files of the device files that are not in the list we intend to deploy
            var removeFiles = deviceFiles
                .Select(f => Path.GetFileName(f.Name))
                .Except(localFiles.Keys
                    .Select(f => Path.GetFileName(f))).ToList();

            // delete those files
            foreach (var file in removeFiles)
            {
                logger?.LogInformation($"Deleting file '{file}'...");
                await meadowConnection.DeleteFile(file, cancellationToken);
            }

            // now send all files with differing CRCs
            foreach (var localFile in localFiles)
            {
                var existing = deviceFiles.FirstOrDefault(f => Path.GetFileName(f.Name) == Path.GetFileName(localFile.Key));

                if (existing != null && existing.Crc != null)
                {
                    var crc = uint.Parse(existing.Crc.Substring(2), System.Globalization.NumberStyles.HexNumber);

                    if (crc == localFile.Value)
                    {   // exists and has a matching CRC, skip it
                        continue;
                    }
                }

            send_file:

                if (!await meadowConnection.WriteFile(localFile.Key, null, cancellationToken))
                {
                    logger?.LogWarning($"Error sending'{Path.GetFileName(localFile.Key)}' - retrying");
                    await Task.Delay(100);
                    goto send_file;
                }
            }

            //on macOS, if we don't write a blank line we lose the writing notifcation for the last file
            logger?.LogInformation(string.Empty);
        }
        finally
        {
            // Renable the runtime, if we've finished deploying.
            isRuntimeEnabled = await meadowConnection.IsRuntimeEnabled();

            if (!isRuntimeEnabled)
            {
                logger?.LogInformation($"Enabling runtime...");

                await meadowConnection.RuntimeDisable(cancellationToken);
            }

            // Wait for the device to realise it's own existence.
            await Task.Delay(2000, cancellationToken);
        }
    }
}