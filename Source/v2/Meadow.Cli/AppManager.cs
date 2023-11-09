using System.Drawing;
using System.Threading;
using Meadow.Hcom;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI;

public static class AppManager
{
    static string[] dllLinkIngoreList = { "System.Threading.Tasks.Extensions.dll" };//, "Microsoft.Extensions.Primitives.dll" };
    static string[] pdbLinkIngoreList = { "System.Threading.Tasks.Extensions.pdb" };//, "Microsoft.Extensions.Primitives.pdb" };

    private static bool MatchingDllExists(string file)
    {
        var root = Path.GetFileNameWithoutExtension(file);
        return File.Exists($"{root}.dll");
    }

    private static bool IsPdb(string file)
    {
        return string.Compare(Path.GetExtension(file), ".pdb", true) == 0;
    }

    private static bool IsXmlDoc(string file)
    {
        if (string.Compare(Path.GetExtension(file), ".xml", true) == 0)
        {
            return MatchingDllExists(file);
        }
        return false;
    }

    public static async Task<Dictionary<string, uint>> GenerateDeployList(IPackageManager packageManager,
        IMeadowConnection connection,
        string localBinaryDirectory,
        bool includePdbs,
        bool includeXmlDocs,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        // TODO: add sub-folder support when HCOM supports it

        logger?.LogInformation($"Generating the list of files to deploy from {localBinaryDirectory}...");

        var localFiles = new Dictionary<string, uint>();

        var auxiliary = Directory.EnumerateFiles(localBinaryDirectory, "*.*", SearchOption.TopDirectoryOnly)
                                       .Where(s => new FileInfo(s).Extension != ".dll")
                                       .Where(s => new FileInfo(s).Extension != ".pdb")
                                       .Where(s => !s.Contains(".DS_Store"));

        foreach (var item in auxiliary)
        {
            var file = Path.Combine(localBinaryDirectory, item);
            if (File.Exists(file))
            {
                await AddToLocalFiles(localFiles, file, includePdbs, includeXmlDocs, cancellationToken);
            }
        }

        if (packageManager.Trimmed && packageManager.TrimmedDependencies != null)
        {
            var trimmedDependencies = packageManager.TrimmedDependencies
                        .Where(x => dllLinkIngoreList.Any(f => x.Contains(f)) == false)
                        .Where(x => pdbLinkIngoreList.Any(f => x.Contains(f)) == false)
                        .ToList();

            //crawl trimmed dependencies
            foreach (var file in trimmedDependencies)
            {
                await AddToLocalFiles(localFiles, file, includePdbs, includeXmlDocs, cancellationToken);
            }

            // Add the Dlls from the TrimmingIgnorelist
            for (int i = 0; i < dllLinkIngoreList.Length; i++)
            {
                //add the files from the dll link ignore list
                if (packageManager.AssemblyDependencies!.Exists(f => f.Contains(dllLinkIngoreList[i])))
                {
                    var dllfound = packageManager.AssemblyDependencies!.FirstOrDefault(f => f.Contains(dllLinkIngoreList[i]));
                    if (!string.IsNullOrEmpty(dllfound))
                    {
                        await AddToLocalFiles(localFiles, dllfound, includePdbs, includeXmlDocs, cancellationToken);
                    }
                }
            }

            if (includePdbs)
            {
                for (int i = 0; i < pdbLinkIngoreList.Length; i++)
                {
                    //add the files from the pdb link ignore list
                    if (packageManager.AssemblyDependencies!.Exists(f => f.Contains(pdbLinkIngoreList[i])))
                    {
                        var pdbFound = packageManager.AssemblyDependencies!.FirstOrDefault(f => f.Contains(pdbLinkIngoreList[i]));
                        if (!string.IsNullOrEmpty(pdbFound))
                        {
                            await AddToLocalFiles(localFiles, pdbFound, includePdbs, includeXmlDocs, cancellationToken);
                        }
                    }
                }
            }
        }
        else
        {
            foreach (var file in packageManager.AssemblyDependencies!)
            {
                // TODO: add any other filtering capability here

                //Populate out LocalFile Dictionary with this entry
                await AddToLocalFiles(localFiles, file, includePdbs, includeXmlDocs, cancellationToken);
            }
        }

        if (localFiles.Count() == 0)
        {
            logger?.LogInformation($"No new files to deploy");
        }

        logger?.LogInformation("Done.");

        return localFiles;
    }

    public static async Task DeployApplication(
        IMeadowConnection connection,
        Dictionary<string, uint> localFiles,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // get a list of files on-device, with CRCs
        var deviceFiles = await connection.GetFileList(true, cancellationToken) ?? Array.Empty<MeadowFileInfo>();

        // get a list of files of the device files that are not in the list we intend to deploy
        var removeFiles = deviceFiles
            .Select(f => Path.GetFileName(f.Name))
            .Except(localFiles.Keys
                .Select(f => Path.GetFileName(f)));

        if (removeFiles.Count() == 0)
        {
            logger.LogInformation($"No files to delete");
        }

        // delete those files
        foreach (var file in removeFiles)
        {
            logger.LogInformation($"Deleting file '{file}'...");
            await connection.DeleteFile(file, cancellationToken);
        }

        // now send all files with differing CRCs
        foreach (var localFile in localFiles)
        {
            // does the file name and CRC match?
            var filename = Path.GetFileName(localFile.Key);

            if (!File.Exists(localFile.Key))
            {
                logger.LogInformation($"{filename} not found" + Environment.NewLine);
                continue;
            }

            if (deviceFiles.Any(d => Path.GetFileName(d.Name) == filename && !string.IsNullOrEmpty(d.Crc) && uint.Parse(d.Crc.Substring(2), System.Globalization.NumberStyles.HexNumber) == localFile.Value))
            {
                logger.LogInformation($"Skipping file (hash match): {filename}" + Environment.NewLine);
                continue;
            }            

            bool success;

            do
            {
                try
                {
                    if (!await connection.WriteFile(localFile.Key, null, cancellationToken))
                    {
                        logger.LogWarning($"Error sending '{Path.GetFileName(localFile.Key)}'.  Retrying.");
                        await Task.Delay(100);
                        success = false;
                    }
                    else
                    {
                        success = true;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"Error sending '{Path.GetFileName(localFile.Key)}' ({ex.Message}).  Retrying.");
                    await Task.Delay(100);
                    success = false;
                }

            } while (!success);
        }
    }

    private static async Task AddToLocalFiles(Dictionary<string, uint> localFiles, string file, bool includePdbs, bool includeXmlDocs, CancellationToken cancellationToken)
    {
        if (!includePdbs && IsPdb(file))
            return;
        if (!includeXmlDocs && IsXmlDoc(file))
            return;

        // read the file data so we can generate a CRC
        using FileStream fs = File.Open(file, FileMode.Open);
        var len = (int)fs.Length;
        var bytes = new byte[len];

        await fs.ReadAsync(bytes, 0, len, cancellationToken);

        var crc = CrcTools.Crc32part(bytes, len, 0);

        if (!localFiles.ContainsKey(file))
            localFiles.Add(file, crc);
    }
}