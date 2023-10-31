using System.Collections.Concurrent;

namespace Meadow.Hcom;

public abstract class MeadowDataProcessor : IDisposable
{
    public EventHandler<MeadowMessageEventArgs>? OnReceiveData;
    public BlockingCollection<byte[]> DebuggerMessages = new BlockingCollection<byte[]>();
    public abstract void Dispose();
}
