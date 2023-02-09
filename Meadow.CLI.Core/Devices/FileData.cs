using System;
using System.IO;

namespace Meadow.CLI.Core.Devices
{
    public record FileData
    {
        public string FullPath { get; private set; } = string.Empty;
        public string FileName => Path.GetFileName(FullPath);
        public uint Crc { get; private set; }
        public int FileSize { get; private set; }

        public static bool TryParse(string info, out FileData data)
        {
            try
            {
                // example data:
                //  /meadow0/App.pdb [0x0c74a77c] 12 KB (9908 bytes)
                var crcStart = info.IndexOf('[') + 1;
                var crcLength = 10; // always 10, formatted hex
                var sizeStart = info.IndexOf('(') + 1;
                var sizeLength = info.IndexOf("bytes") - sizeStart;

                data = new FileData
                {
                    FullPath = info.Substring(0, crcStart - 2).Trim(),
                    Crc = Convert.ToUInt32(info.Substring(crcStart, crcLength), 16),
                    FileSize = int.Parse(info.Substring(sizeStart, sizeLength))
                };
                return true;
            }
            catch
            {
                data = new FileData();
                return false;
            }
        }
    }
}