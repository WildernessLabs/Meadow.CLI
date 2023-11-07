public class MeadowFileInfo
{
    public string Name { get; private set; } = default!;
    public long? Size { get; private set; }
    public string? Crc { get; private set; }

    public override string ToString()
    {
        return $"{Path.GetFileName(Name)} [0x{Crc:8x}]";
    }

    public static MeadowFileInfo? Parse(string info)
    {
        MeadowFileInfo? mfi = null;

        // parse the input to a file info
        if (info.StartsWith("/"))
        {
            mfi = new MeadowFileInfo();

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