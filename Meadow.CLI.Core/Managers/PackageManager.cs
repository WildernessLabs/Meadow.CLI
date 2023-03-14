using Meadow.CLI.Core.Identity;
using MeadowCLI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Meadow.CLI.Core
{
    public class PackageManager
    {
        public PackageManager(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<PackageManager>();
        }

        private readonly ILogger _logger;

        public string CreatePackage(string applicationPath, string osVersion)
        {
            var zipFile = Path.Combine(Environment.CurrentDirectory, $"{DateTime.UtcNow.ToString("yyyyMMdd")}{DateTime.UtcNow.Millisecond.ToString()}.mpak");

            if (!Directory.Exists(applicationPath))
            {
                throw new ArgumentException("Invalid applicationPath");
            }

            var osFilePath = Path.Combine(DownloadManager.FirmwareDownloadsFilePathRoot, osVersion);
            if (!Directory.Exists(osFilePath))
            {
                throw new ArgumentException("osVersion not found. Please download.");
            }

            var osFiles = Directory.GetFiles(osFilePath);
            var files = Directory.GetFiles(applicationPath);

            using (var archive = ZipFile.Open(zipFile, ZipArchiveMode.Create))
            {
                foreach (var fPath in files)
                {
                    archive.CreateEntryFromFile(fPath, Path.Combine("app", Path.GetFileName(fPath)));
                }

                foreach (var fPath in osFiles)
                {
                    archive.CreateEntryFromFile(fPath, Path.Combine("os", Path.GetFileName(fPath)));
                }
            }

            return zipFile;
        }
    }
}
