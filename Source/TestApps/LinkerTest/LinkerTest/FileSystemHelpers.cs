namespace LinkerTest;

public static class FileSystemHelpers
{
    public static void CopyFileWithOptionalPdb(string sourcePath, string destinationPath, bool includePdbs)
    {
        File.Copy(sourcePath, destinationPath, overwrite: true);

        if (includePdbs)
        {
            var pdbFile = Path.ChangeExtension(sourcePath, "pdb");
            if (File.Exists(pdbFile))
            {
                File.Copy(pdbFile, Path.Combine(Path.GetDirectoryName(destinationPath), Path.GetFileName(pdbFile)), overwrite: true);
            }
        }
    }

    public static void CleanupAndCreateDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
        Directory.CreateDirectory(directoryPath);
    }
}