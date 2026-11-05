namespace Meadow.Hcom;

/// <summary>
/// Thrown when the device reports that a file it received could not be written or verified,
/// for example because its file system is full.
/// </summary>
public class DeviceFileWriteException : IOException
{
    /// <summary>
    /// The file name on the device.
    /// </summary>
    public string MeadowFileName { get; }

    /// <summary>
    /// The error text the device sent.
    /// </summary>
    public string DeviceMessage { get; }

    internal DeviceFileWriteException(string meadowFileName, string deviceMessage)
        : base($"Device failed to write '{meadowFileName}': {deviceMessage}")
    {
        MeadowFileName = meadowFileName;
        DeviceMessage = deviceMessage;
    }
}
