namespace Meadow.Cloud.Client;

public class MeadowCloudUserAgent
{
    public static readonly MeadowCloudUserAgent Cli = new ("Meadow.Cli");
    public static readonly MeadowCloudUserAgent Workbench = new ("Meadow.Workbench");

    public string UserAgent { get; }

    public MeadowCloudUserAgent(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            throw new ArgumentException($"'{nameof(userAgent)}' cannot be null or whitespace.", nameof(userAgent));
        }

        UserAgent = userAgent;
    }

    public override string ToString() => UserAgent;
    public override int GetHashCode() => UserAgent.GetHashCode();

    public static implicit operator string(MeadowCloudUserAgent userAgent) => userAgent.UserAgent;
    public static implicit operator MeadowCloudUserAgent(string userAgent) => new (userAgent);
}
