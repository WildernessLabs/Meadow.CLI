using System;
using System.IO;
using System.IO.Hashing;
using System.Threading.Tasks;

public class Hasher
{
    public async Task<string> CalculateCrc32(string filePath)
    {
        var crc32 = new Crc32();

        using (var fs = File.OpenRead(filePath))
        {
            await crc32.AppendAsync(fs);
        }

        var checkSum = crc32.GetCurrentHash();
        Array.Reverse(checkSum);
        return BitConverter.ToString(checkSum).Replace("-", "").ToLower();
    }
}