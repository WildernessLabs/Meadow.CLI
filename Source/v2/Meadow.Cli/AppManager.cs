using System.Drawing;
using System.Threading;
using Meadow.Hcom;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI;

public static class AppManager
{
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
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // TODO: add sub-folder support when HCOM supports it

        logger.LogInformation("Generating the list of files to deploy...");

        var localFiles = new Dictionary<string, uint>();

        var additionFilesList = new string[]
        {
            "app.config.json",
            "meadow.config.yaml",
            "wifi.config.yaml",
        };

        string[] dllLinkIngoreList = { "System.Threading.Tasks.Extensions.dll" };//, "Microsoft.Extensions.Primitives.dll" };
        string[] pdbLinkIngoreList = { "System.Threading.Tasks.Extensions.pdb" };//, "Microsoft.Extensions.Primitives.pdb" };

        // get a list of files to send
        var dependencies = packageManager.GetDependencies(new FileInfo(Path.Combine(localBinaryDirectory, "App.dll")));

        if (packageManager.Trimmed && packageManager.TrimmedDependencies != null)
        {
            var trimmedDependencies = packageManager.TrimmedDependencies.Where(x => x.Contains("App.") == false)
                        .Where(x => dllLinkIngoreList.Any(f => x.Contains(f)) == false)
                        .Where(x => pdbLinkIngoreList.Any(f => x.Contains(f)) == false)
                        .ToList();

            //crawl trimmed dependencies
            foreach (var file in trimmedDependencies)
            {
                if (!includePdbs && IsPdb(file))
                    continue;
                if (!includeXmlDocs && IsXmlDoc(file))
                    continue;

                await AddToLocalFiles(localFiles, file, cancellationToken);
            }
        }
        else
        {
            foreach (var file in dependencies)
            {
                // TODO: add any other filtering capability here

                if (!includePdbs && IsPdb(file))
                    continue;
                if (!includeXmlDocs && IsXmlDoc(file))
                    continue;

                //Populate out LocalFile Dictionary with this entry
                await AddToLocalFiles(localFiles, file, cancellationToken);
            }
        }

        foreach (var item in additionFilesList)
        {
            var file = Path.Combine(localBinaryDirectory, item);
            if (File.Exists(file))
            {
                await AddToLocalFiles(localFiles, file, cancellationToken);
            }
        }

        if (localFiles.Count() == 0)
        {
            logger.LogInformation($"No new files to deploy");
        }

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

        foreach (var deviceFile in deviceFiles)
        {
            var exists = localFiles.FirstOrDefault(l => Path.GetFileName(deviceFile.Name) == Path.GetFileName(l.Key));
            if (deviceFile.Crc != null)
            {
                if (uint.Parse(deviceFile.Crc.Substring(2), System.Globalization.NumberStyles.HexNumber) == exists.Value)
                {
                    // exists and has a matching CRC, skip it
                    localFiles.Remove(exists.Key);
                }
            }
        }

        return localFiles;
    }

    public static async Task DeployApplication(
        IMeadowConnection connection,
        Dictionary<string, uint> localFiles,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // now send all files with differing CRCs
        foreach (var localFile in localFiles)
        {
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

    private static async Task AddToLocalFiles(Dictionary<string, uint> localFiles, string file, CancellationToken cancellationToken)
    {
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