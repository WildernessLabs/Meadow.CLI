using Meadow.Hcom;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.Cli;

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

    public static async Task DeployApplication(
        IMeadowConnection connection,
        string localBinaryDirectory,
        bool includePdbs,
        bool includeXmlDocs,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // in order to deploy, the runtime must be disabled
        var wasRuntimeEnabled = await connection.IsRuntimeEnabled();

        if (wasRuntimeEnabled)
        {
            logger.LogInformation("Disabling runtime...");

            await connection.RuntimeDisable(cancellationToken);
        }

        // TODO: add sub-folder support when HCOM supports it

        var localFiles = new Dictionary<string, uint>();

        // get a list of files to send
        logger.LogInformation("Generating the list of files to deploy...");
        foreach (var file in Directory.GetFiles(localBinaryDirectory))
        {
            // TODO: add any other filtering capability here

            if (!includePdbs && IsPdb(file)) continue;
            if (!includeXmlDocs && IsXmlDoc(file)) continue;

            // read the file data so we can generate a CRC
            using FileStream fs = File.Open(file, FileMode.Open);
            var len = (int)fs.Length;
            var bytes = new byte[len];

            await fs.ReadAsync(bytes, 0, len, cancellationToken);

            var crc = CrcTools.Crc32part(bytes, len, 0);

            localFiles.Add(file, crc);
        }

        // get a list of files on-device, with CRCs
        var deviceFiles = await connection.GetFileList(true, cancellationToken);

        // get a list of files of the device files that are not in the list we intend to deploy
        var removeFiles = deviceFiles
            .Select(f => f.Name)
            .Except(localFiles.Keys
                .Select(f => Path.GetFileName(f)));

        // delete those files
        foreach (var file in removeFiles)
        {
            await connection.DeleteFile(file, cancellationToken);
        }

        // erase all files on device not in list of files to send

        // send any file that has a different CRC


        if (wasRuntimeEnabled)
        {
            // restore runtime state
            logger.LogInformation("Enabling runtime...");

            await connection.RuntimeEnable(cancellationToken);
        }

    }
}
