namespace Meadow.Cli;

public partial class PackageManager
{
    private record BuildOptions
    {
        public DeployOptions? Deploy { get; set; }

        public record DeployOptions
        {
            public List<string>? NoLink { get; set; }
            public bool? IncludePDBs { get; set; }
        }
    }
}
