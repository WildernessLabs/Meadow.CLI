﻿using Meadow.Software;

namespace Meadow.SoftwareManager.Integration.Tests;

public class F7FirmwareDownloadManagerTests
{
    [Fact]
    public async void GetLatestAvailableVersionTest()
    {
        var downloadManager = new F7FirmwareDownloadManager("CLI.Test");
        var result = await downloadManager.GetLatestAvailableVersion();
    }
}