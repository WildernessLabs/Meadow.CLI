using Microsoft.Extensions.Logging;

namespace Meadow.Hcom;

public class DownloadFileStream : Stream, IDisposable
{
    private readonly ILogger _logger;
    private readonly Stream _stream;

    private long _position;
    private DateTimeOffset _lastLog;
    private long _lastPosition;

    public DownloadFileStream(Stream stream, ILogger logger)
    {
        _stream = stream;
        _logger = logger;
        _lastLog = DateTimeOffset.Now;
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
        var now = DateTimeOffset.Now;
        if (_lastLog.AddSeconds(5) < now)
        {
            LogPosition();
            _lastLog = now;
        }
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
        LogPosition();
        base.Dispose(disposing);
    }

    private void LogPosition()
    {
        if (_position == _lastPosition)
        {
            return;
        }

        if (_position < 1024)
        {
            _logger.LogInformation("Downloaded {position} bytes", _position);
            _lastPosition = _position;
        }
        else if (_position < (1024 * 1024))
        {
            _logger.LogInformation("Downloaded {position} KiB", Math.Round(_position / 1024M, 2, MidpointRounding.ToEven));
            _lastPosition = _position;
        }
        else
        {
            _logger.LogInformation("Downloaded {position} MiB", Math.Round(_position / 1024M / 1024M, 2, MidpointRounding.ToEven));
            _lastPosition = _position;
        }
    }
}
