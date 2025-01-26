using System;
using System.IO;

namespace Meadow.Software;

internal class DownloadFileStream : Stream, IDisposable
{
    public event EventHandler<long> DownloadProgress = default!;

    private readonly Stream _stream;

    private long _position;

    public DownloadFileStream(Stream stream)
    {
        _stream = stream;

        DownloadProgress?.Invoke(this, 0);
    }

    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _stream.Length;
    public override long Position { get => _position; set => throw new NotImplementedException(); }

    public override void Flush()
    {
        throw new NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var b = _stream.Read(buffer, offset, count);
        _position += b;

        DownloadProgress?.Invoke(this, _position);

        return b;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        _stream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
