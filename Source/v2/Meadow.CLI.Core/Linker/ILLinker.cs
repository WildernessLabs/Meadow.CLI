using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LinkerTest
{
    internal class ILLinker
    {
        readonly ILogger? _logger;

        public ILLinker(ILogger? logger = null)
        {
            _logger = logger;
        }

        public async Task RunILLink(
            string illinkerDllPath,
            string descriptorXmlPath,
            string noLinkArgs,
            string prelinkAppPath,
            string prelinkDir,
            string postlinkDir)
        {
            if (!File.Exists(illinkerDllPath))
            {
                throw new FileNotFoundException("Cannot run trimming operation, illink.dll not found");
            }

            //original
            //var monolinker_args = $"\"{illinkerDllPath}\" -x \"{descriptorXmlPath}\" {noLinkArgs} --skip-unresolved --deterministic --keep-facades true --ignore-descriptors true -b true -c link -o \"{postlinkDir}\" -r \"{prelinkAppPath}\" -a \"{prelink_os}\" -d \"{prelinkDir}\"";

            var monolinker_args = $"\"{illinkerDllPath}\"" +
                $" -x \"{descriptorXmlPath}\" " + //link files in the descriptor file (needed)
                $"{noLinkArgs} " + //arguments to skip linking - will be blank if we are linking
                $"-r \"{prelinkAppPath}\" " + //link the app in the prelink folder (needed)
                $"--skip-unresolved true " + //skip unresolved references (needed -hangs without)
                $"--deterministic true " + //make deterministic (to avoid pushing unchanged files to the device)
                $"--keep-facades true " + //keep facades (needed - will skip key libs without)
                $"-b true " + //Update debug symbols for each linked module (needed - will skip key libs without)
                $"-o \"{postlinkDir}\" " + //output directory


                //old
                //$"--ignore-descriptors false " + //ignore descriptors (doesn't appear to impact behavior)
                //$"-c link " + //link framework assemblies
                //$"-d \"{prelinkDir}\"" //additional folder to link (not needed)

                //experimental
                //$"--explicit-reflection true " + //enable explicit reflection (throws an exception with it)
                //$"--keep-dep-attributes true " + //keep dependency attributes (files are slightly larger with, doesn't fix dependency issue)
                "";

            _logger?.Log(LogLevel.Information, "Trimming assemblies");

            using (var process = new Process())
            {
                process.StartInfo.WorkingDirectory = Directory.GetDirectoryRoot(illinkerDllPath);
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

                    _logger?.Log(LogLevel.Debug, "StandardOutput Contains: " + stdOutReaderResult);
                }

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger?.Log(LogLevel.Debug, $"Trimming failed - ILLinker execution error!\nProcess Info: {process.StartInfo.FileName} {process.StartInfo.Arguments} \nExit Code: {process.ExitCode}");
                    throw new Exception("Trimming failed");
                }
            }
        }
    }
}