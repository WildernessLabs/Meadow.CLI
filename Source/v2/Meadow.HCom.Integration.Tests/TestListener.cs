using Meadow.Hcom;

namespace Meadow.HCom.Integration.Tests
{
    public class TestListener : IConnectionListener
    {
        public List<string> StdOut { get; } = new List<string>();
        public List<string> StdErr { get; } = new List<string>();
        public List<string> Messages { get; } = new List<string>();
        public Dictionary<string, string> DeviceInfo { get; private set; } = new Dictionary<string, string>();
        public List<string> TextList { get; } = new List<string>();
        public string? LastError { get; set; }
        public int? LastRequestConcluded { get; set; }

        public void OnTextMessageConcluded(int requestType)
        {
            LastRequestConcluded = requestType;
        }

        public void OnStdOutReceived(string message)
        {
            StdOut.Add(message);
        }

        public void OnStdErrReceived(string message)
        {
            StdErr.Add(message);
        }

        public void OnInformationMessageReceived(string message)
        {
            Messages.Add(message);
        }

        public void OnDeviceInformationMessageReceived(Dictionary<string, string> deviceInfo)
        {
            DeviceInfo = deviceInfo;
        }

        public void OnTextListReceived(string[] list)
        {
            TextList.Clear();
            TextList.AddRange(list);
        }

        public void OnErrorTextReceived(string message)
        {
            LastError = message;
        }

        public void OnFileError()
        {
            throw new Exception(LastError);
        }
    }
}