namespace Meadow.Hcom;

// For data received due to a CLI request these provide a secondary
// type of identification. The primary being the protocol request value
public enum MeadowMessageType
{
    AppOutput,
    ErrOutput,
    DeviceInfo,
    FileListTitle,
    FileListMember,
    FileListCrcMember,
    Data,
    InitialFileData,
    MeadowTrace,
    SerialReconnect,
    Accepted,
    Concluded,
    DownloadStartOkay,
    DownloadStartFail,
    DownloadFailed,
    DevicePublicKey
}
