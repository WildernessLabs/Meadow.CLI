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

        public async Task RunILLink(string illinker_path,
            string descriptor_path,
            string no_link_args,
            string prelink_app,
            string prelink_os,
            string prelink_dir,
            string postlink_dir)
        {
            if (!File.Exists(illinker_path))
            {
                throw new FileNotFoundException("Cannot run trimming operation, illink.dll not found.");
            }

            //original
            //var monolinker_args = $"\"{illinker_path}\" -x \"{descriptor_path}\" {no_link_args}  --skip-unresolved --deterministic --keep-facades true --ignore-descriptors true -b true -c link -o \"{postlink_dir}\" -r \"{prelink_app}\" -a \"{prelink_os}\" -d \"{prelink_dir}\"";

            var monolinker_args = $"\"{illinker_path}\" -x \"{descriptor_path}\" --skip-unresolved true --deterministic true --keep-facades true --ignore-descriptors false -b true -c link -o \"{postlink_dir}\" -a \"{prelink_app}\" -a \"{prelink_os}\" -d \"{prelink_dir}\"";

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

                /*
                string stdErrorReaderResult;
                using (StreamReader stdErrorReader = process.StandardError)
                {
                    stdErrorReaderResult = await stdErrorReader.ReadToEndAsync();
                    if (!string.IsNullOrEmpty(stdErrorReaderResult))
                    {
                        logger?.Log(LogLevel.Debug, "StandardError Contains: " + stdErrorReaderResult);
                    }
                }*/

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
