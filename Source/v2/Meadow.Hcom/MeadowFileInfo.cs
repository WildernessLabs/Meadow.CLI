public class MeadowFileInfo
{
    public string Name { get; set; } = default!;
    public long? Size { get; private set; }
    public string? Crc { get; private set; }
    public bool IsDirectory { get; private set; } = default!;
    public bool IsSummary { get; private set; } = default!;

    public static MeadowFileInfo? Parse(string info)
    {
        MeadowFileInfo? mfi = new MeadowFileInfo(); ;

        if (info.StartsWith("Directory:")) // Directory we are currently in
        {
            mfi.IsDirectory = true;
            var indexOfColon = info.IndexOf(':') + 1;
            mfi.Name = info.Substring(indexOfColon, info.Length - indexOfColon).Trim();
        }
        else if (info.StartsWith("/")) // must be a sub-directory of the current directory
        {
            mfi.Name = info.Substring(1, info.Length - 1).Trim() + "/";
        }
        else if (info.StartsWith("A total of")) // must be a sub-directory of the current directory
        {
            mfi.IsSummary = true;
            mfi.Name = info;
        }
        else // Must be a file in our current directory
        {
            // "/meadow0/App.deps.json [0xa0f6d6a2] 28 KB (26575 bytes)"
            var indexOfSquareBracket = info.IndexOf('[');
            if (indexOfSquareBracket <= 0)
            {
                mfi.Name = info;
            }
            else
            {
                mfi.Name = info.Substring(0, indexOfSquareBracket - 1).Trim();
                mfi.Crc = info.Substring(indexOfSquareBracket + 1, 10);
                var indexOfParen = info.IndexOf("(");
                var end = info.IndexOf(' ', indexOfParen);
                mfi.Size = int.Parse(info.Substring(indexOfParen + 1, end - indexOfParen));
            }
        }
        return mfi;
    }
}