using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LinkerTest
{
    internal class ILLinker
    {
        readonly ILogger? logger;

        public ILLinker(ILogger? logger = null)
        {
            this.logger = logger;
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
                throw new FileNotFoundException("Cannot run trimming operation, illink.dll not found.");
            }

            //original
            //var monolinker_args = $"\"{illinkerDllPath}\" -x \"{descriptorXmlPath}\" {noLinkArgs} --skip-unresolved --deterministic --keep-facades true --ignore-descriptors true -b true -c link -o \"{postlinkDir}\" -r \"{prelinkAppPath}\" -a \"{prelink_os}\" -d \"{prelinkDir}\"";

            var monolinker_args = $"\"{illinkerDllPath}\" -x \"{descriptorXmlPath}\" {noLinkArgs} --skip-unresolved true --deterministic true --keep-facades true --ignore-descriptors false -b true -c link -o \"{postlinkDir}\" -r \"{prelinkAppPath}\" -d \"{prelinkDir}\"";

            logger?.Log(LogLevel.Information, "Trimming assemblies");

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

                    logger?.Log(LogLevel.Debug, "StandardOutput Contains: " + stdOutReaderResult);
                }

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    logger?.Log(LogLevel.Debug, $"Trimming failed - ILLinker execution error!\nProcess Info: {process.StartInfo.FileName} {process.StartInfo.Arguments} \nExit Code: {process.ExitCode}");
                    throw new Exception("Trimming failed");
                }
            }
        }
    }
}