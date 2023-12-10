using System.Diagnostics;

namespace LinkerTest
{
    internal class Program
    {
        private static readonly string _meadowAssembliesPath = @"C:\Users\adria\AppData\Local\WildernessLabs\Firmware\1.6.0.1\meadow_assemblies\";

        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            //  await OtherLink();

            //  return;

            var linker = new MeadowLinker(_meadowAssembliesPath);

            string fileToLink = @"H:\WL\Meadow.ProjectLab\Source\ProjectLab_Demo\bin\Debug\netstandard2.1\App.dll";

            await linker.Trim(new FileInfo(fileToLink), true);
        }

        static async Task OtherLink()
        {
            var monolinker_args = @"""H:\WL\Meadow.CLI\Meadow.CLI.Classic\bin\Debug\lib\illink.dll"" -x ""H:\WL\Meadow.CLI\Meadow.CLI.Classic\bin\Debug\lib\meadow_link.xml""   --skip-unresolved --deterministic --keep-facades true --ignore-descriptors true -b true -c link -o ""H:\WL\Meadow.ProjectLab\Source\ProjectLab_Demo\bin\Debug\netstandard2.1\postlink_bin"" -r ""H:\WL\Meadow.ProjectLab\Source\ProjectLab_Demo\bin\Debug\netstandard2.1\prelink_bin\App.dll"" -a ""H:\WL\Meadow.ProjectLab\Source\ProjectLab_Demo\bin\Debug\netstandard2.1\prelink_bin\Meadow.dll"" -d ""H:\WL\Meadow.ProjectLab\Source\ProjectLab_Demo\bin\Debug\netstandard2.1\prelink_bin""";

            Console.WriteLine("Trimming assemblies to reduce size (may take several seconds)...");

            using (var process = new Process())
            {
                process.StartInfo.FileName = "dotnet";
                process.StartInfo.Arguments = monolinker_args;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();

                // To avoid deadlocks, read the output stream first and then wait
                string stdOutReaderResult;
                using (StreamReader stdOutReader = process.StandardOutput)
                {
                    stdOutReaderResult = await stdOutReader.ReadToEndAsync();
                    Console.WriteLine("StandardOutput Contains: " + stdOutReaderResult);
                }
            }
        }
    }
}
