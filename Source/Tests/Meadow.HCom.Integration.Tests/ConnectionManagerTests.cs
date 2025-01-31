using Meadow.Hcom;
using System.Diagnostics;

namespace Meadow.HCom.Integration.Tests;

public class SettingsManagerTests
{
    [Fact]
    public void TestSerializingWiFiConfig()
    {
        var m = new MeadowSettingsManager(null);
        m.WriteWiFiSettings("my-SSID", "Pa$$w0rd");
    }
}

public class ConnectionManagerTests
{
    public string ValidPortName { get; } = "COM3";

    private SerialConnection GetConnection(string port)
    {
        // windows sucks and doesn't release the port, even after Dispose,
        // so this is a workaround
        return ConnectionManager
            .GetConnection<SerialConnection>(port);
    }

    [Fact]
    public void TestInvalidPortName()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            GetConnection("InvalidPortName");
        });
    }

    [Fact]
    public async Task TestDeviceReset()
    {
        var c = GetConnection(ValidPortName);
        var device = await c.Attach();

        if (device == null)
        {
            Assert.Fail("no device");
            return;
        }

        await device.Reset();

        // just getting here with no exception means success
    }

    [Fact]
    public async Task TestReadFileBadLocalPath()
    {
        var c = GetConnection(ValidPortName);
        var device = await c.Attach();

        if (device == null)
        {
            Assert.Fail("no device");
            return;
        }

        var enabled = await device.IsRuntimeEnabled();

        if (enabled)
        {
            await device.RuntimeDisable();
        }

        var dest = "c:\\invalid_local_path\\app.config.yaml";

        await Assert.ThrowsAsync<DirectoryNotFoundException>(async () =>
        {
            await device.ReadFile("app.config.yaml", dest);
        });
    }

    [Fact]
    public async Task TestReadFilePositive()
    {
        var c = GetConnection(ValidPortName);
        var device = await c.Attach();

        if (device == null)
        {
            Assert.Fail("no device");
            return;
        }

        var enabled = await device.IsRuntimeEnabled();

        if (enabled)
        {
            await device.RuntimeDisable();
        }

        var dest = "f:\\temp\\app.config.yaml"; // <-- this needs to be valid on the test machine
        if (File.Exists(dest)) File.Delete(dest);
        Assert.False(File.Exists(dest));

        var result = await device.ReadFile("app.config.yaml", dest);
        Assert.True(result);
        Assert.True(File.Exists(dest));
    }

    [Fact]
    public async Task TestGetDeviceInfo()
    {
        var c = GetConnection(ValidPortName);
        var device = await c.Attach();

        if (device == null)
        {
            Assert.Fail("no device");
            return;
        }

        var info = await device.GetDeviceInfo();
        Assert.NotNull(info);
        Assert.True(info.Properties.Any());
    }

    [Fact]
    public async Task TestGetFileListWithoutCrcs()
    {
        var c = GetConnection(ValidPortName);
        var device = await c.Attach();

        if (device == null)
        {
            Assert.Fail("no device");
            return;
        }
        var files = await device.GetFileList("/meadow0/", false);
        Assert.NotNull(files);
        Assert.True(files.Any());
        Assert.True(files.All(f => f.Name != null));
        Assert.True(files.All(f => f.Crc == null));
        Assert.True(files.All(f => f.Size == null));
    }

    [Fact]
    public async Task TestGetFileListWithCrcs()
    {
        var c = GetConnection(ValidPortName);
        var device = await c.Attach();

        if (device == null)
        {
            Assert.Fail("no device");
            return;
        }
        var files = await device.GetFileList("/meadow0/", true);
        Assert.NotNull(files);
        Assert.True(files.Any());
        Assert.True(files.All(f => f.Name != null));
        Assert.True(files.All(f => f.Crc != null));
        Assert.True(files.All(f => f.Size != null));
    }

    [Fact]
    public async Task TestRuntimeEnableAndDisable()
    {
        var c = GetConnection(ValidPortName);
        var device = await c.Attach();

        if (device == null)
        {
            Assert.Fail("no device");
            return;
        }
        // get the current runtime state
        var start = await device.IsRuntimeEnabled();

        if (start)
        {
            Debug.WriteLine("*** Runtime started enabled.");
            Debug.WriteLine("*** Disabling...");
            await device.RuntimeDisable();
            Debug.WriteLine("*** Enabling...");
            await device.RuntimeEnable();

            Assert.True(await device.IsRuntimeEnabled());
        }
        else
        {
            Debug.WriteLine("*** Runtime started disabled.");
            Debug.WriteLine("*** Enabling...");
            await device.RuntimeEnable();
            Debug.WriteLine("*** Disabling...");
            await device.RuntimeDisable();

            Assert.False(await device.IsRuntimeEnabled());
        }
    }

    [Fact]
    public async Task TestRtcFunctions()
    {
        var c = GetConnection(ValidPortName);
        var device = await c.Attach();

        if (device == null)
        {
            Assert.Fail("no device");
            return;
        }
        // get the current runtime state
        var time = await device.GetRtcTime();

        var newTime = time.Value.AddMinutes(17);
        await device.SetRtcTime(newTime);

        time = await device.GetRtcTime();

        // should be withing a few seconds of newTime
        var delta = Math.Abs(time.Value.Ticks - newTime.Ticks);
        var deltaOffset = new TimeSpan(delta);
        Assert.True(deltaOffset.TotalSeconds < 5);
    }
}

