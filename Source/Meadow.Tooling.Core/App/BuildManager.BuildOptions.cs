using System.Collections.Generic;

namespace Meadow.CLI;

public partial class BuildManager
{
    private record BuildOptions
    {
        public DeployOptions Deploy { get; set; }

        public record DeployOptions
        {
            public List<string> NoLink { get; set; }
            public bool? IncludePDBs { get; set; }
        }
    }
}