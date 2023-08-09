namespace Meadow.Hcom
{
    public interface IMeadowConnection
    {
        event EventHandler<string> FileReadCompleted;
        event EventHandler<Exception> FileException;
        event EventHandler<Exception> ConnectionError;

        string Name { get; }
        IMeadowDevice? Device { get; }
        Task<IMeadowDevice?> Attach(CancellationToken? cancellationToken = null, int timeoutSeconds = 10);
        Task WaitForMeadowAttach(CancellationToken? cancellationToken = null);
        ConnectionState State { get; }

        // internal stuff that probably needs to get moved to anotehr interface
        void AddListener(IConnectionListener listener);
        void RemoveListener(IConnectionListener listener);
        void EnqueueRequest(IRequest command);
    }
}