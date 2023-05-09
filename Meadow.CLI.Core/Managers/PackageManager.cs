using Meadow.CLI.Core.Identity;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
            string[]? osFiles = null;
            string[]? appFiles = null;

            if(!string.IsNullOrEmpty(applicationPath))
            {
                if (!File.Exists(applicationPath) && !Directory.Exists(applicationPath))
                {
                    throw new ArgumentException($"Invalid applicationPath: {applicationPath}");
                }
                else
                {
                    var fi = new FileInfo(applicationPath);
                    if(fi.Attributes.HasFlag(FileAttributes.Directory))
                    {
                        appFiles = Directory.GetFiles(applicationPath);
                    }
                    else
                    {
                        appFiles = new List<string>{ fi.FullName }.ToArray();
                    }
                }
            }

            if(!string.IsNullOrEmpty(osVersion))
            {
                var osFilePath = Path.Combine(DownloadManager.FirmwareDownloadsFilePathRoot, osVersion);
                if (!Directory.Exists(osFilePath))
                {
                    throw new ArgumentException($"osVersion {osVersion} not found. Please download.");
                }
                osFiles = Directory.GetFiles(osFilePath);
            }
            
            if(appFiles != null || osFiles !=null)
            {
                var zipFile = Path.Combine(Environment.CurrentDirectory, $"{DateTime.UtcNow.ToString("yyyyMMdd")}{DateTime.UtcNow.Millisecond.ToString()}.mpak");
                using (var archive = ZipFile.Open(zipFile, ZipArchiveMode.Create))
                {
                    if(appFiles != null)
                    {
                        foreach (var fPath in appFiles)
                        {
                            archive.CreateEntryFromFile(fPath, Path.Combine("app", Path.GetFileName(fPath)));
                        }
                    }
                    
                    if(osFiles != null)
                    {
                        foreach (var fPath in osFiles)
                        {
                            archive.CreateEntryFromFile(fPath, Path.Combine("os", Path.GetFileName(fPath)));
                        }
                    }
                }
                return zipFile;
            }
            else
            {
                _logger.LogError("Application Path or OS Version was not specified.");
                return string.Empty;
            }
        }
    }
}
