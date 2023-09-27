namespace Meadow.Cli;

public interface IPackageManager
{
    bool BuildApplication(string projectFilePath, string configuration = "Release");
    Task TrimApplication(
        FileInfo applicationFilePath,
        bool includePdbs = false,
        IList<string>? noLink = null,
        CancellationToken cancellationToken = default);
}
