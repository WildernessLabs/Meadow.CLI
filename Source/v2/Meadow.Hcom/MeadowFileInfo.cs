public class MeadowFileInfo
{
    public string Name { get; private set; } = default!;
    public long? Size { get; private set; }
    public string? Crc { get; private set; }
    public bool IsDirectory { get; private set; }

    public override string ToString()
    {
        return $"{(IsDirectory ? "/" : "")}{Name}";
    }

    public static MeadowFileInfo? Parse(string info)
    {
        MeadowFileInfo? mfi = null;

        // parse the input to a file info
        if (info.StartsWith("/"))
        {
            mfi = new MeadowFileInfo();

            mfi.Name = info.Substring(1);
            mfi.IsDirectory = true;
        }
        else
        {
            // v2 file lists have changed

            if (info.StartsWith("Directory:"))
            {
                // this is the first line and contains the directory name being parsed
                return mfi;
            }
            else if (info.StartsWith("A total of"))
            {
                return mfi;
            }

            mfi = new MeadowFileInfo();

            var indexOfSquareBracket = info.IndexOf('[');
            if (indexOfSquareBracket <= 0)
            {
                mfi.Name = info.Trim();
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