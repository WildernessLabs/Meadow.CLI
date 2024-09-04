namespace Meadow.Hcom;

public interface IConnectionListener
{
    void OnInformationMessageReceived(string message);
    void OnStdOutReceived(string message);
    void OnStdErrReceived(string message);
    void OnDeviceInformationMessageReceived(Dictionary<string, string> deviceInfo);
    void OnTextListReceived(string[] list);
    void OnErrorTextReceived(string message);
    void OnFileError();
    void OnTextMessageConcluded(int requestType);
}