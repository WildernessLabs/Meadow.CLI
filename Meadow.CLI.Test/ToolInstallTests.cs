using System.Diagnostics;
using Meadow.CLI.Core;
using NUnit.Framework;

namespace Meadow.CLI.Test
{
    [TestFixture]
    [SingleThreaded]
    public class ToolInstallTests
    {
        [Test]
        [Order(1)]
        public void ToolUpdate()
        {
            // Uninstall any versions already on there
            using Process ui = LaunchDotNetTool("uninstall");
            var uiresult = ui?.StandardOutput.ReadToEnd();
            var uierror = ui?.StandardError.ReadToEnd();

            if (!uierror.Contains("could not be found"))
            {
                // Make sure error is blank
                Assert.True(string.IsNullOrEmpty(uierror));
            }

            using Process pi = LaunchDotNetTool("install");
            var piresult = pi?.StandardOutput.ReadToEnd();
            var pierror = pi?.StandardError.ReadToEnd();

            // Make sure error is blank
            Assert.True(string.IsNullOrEmpty(pierror));

            // Run the command line call to update the tool.
            using Process pu = LaunchDotNetTool("update", "1.1.0");
            var uresult = pu?.StandardOutput.ReadToEnd();
            var uerror = pu?.StandardError.ReadToEnd();

            // Make sure error is blank
            Assert.True(string.IsNullOrEmpty(uerror));

            // Make sure result isn't blank
            Assert.False(string.IsNullOrEmpty(uresult));

            // Assert that the tool was installed successfully.
            Assert.True(uresult.Contains($"was successfully updated from version '1.0.0' to version '1.1.0'"));

        }

        [Test]
        [Order(2)]
        public void ToolInstall()
        {
            // Run the command line call to install the tool.
            using Process proc = LaunchDotNetTool("install");
            var result = proc?.StandardOutput.ReadToEnd();
            var error = proc?.StandardError.ReadToEnd();

            // Make sure result isn't blank
            Assert.True(string.IsNullOrEmpty(error));

            // Make sure result isn't blank
            Assert.False(string.IsNullOrEmpty(result));

            // Assert that the tool was installed successfully.
            Assert.True(result.Contains("was successfully installed"));
        }

        [Test]
        [Order(3)]
        public void ToolUninstall()
        {
            // Run the command line call to uninstall the tool.
            using Process proc = LaunchDotNetTool("uninstall");
            var result = proc?.StandardOutput.ReadToEnd();
            var error = proc?.StandardError.ReadToEnd();

            // Make sure error is blank
            Assert.True(string.IsNullOrEmpty(error));

            // Make sure result isn't blank
            Assert.False(string.IsNullOrEmpty(result));

            // Assert that the tool was installed successfully.
            Assert.True(result.Contains("was successfully uninstalled"));
        }

        private static Process LaunchDotNetTool(string command, string version = "1.0.0", int milliSecondWait = 15000)
        {
            // Run the command line call to install the tool.
            var psi = new ProcessStartInfo()
            {
                FileName = "dotnet",
                Arguments = command.Contains("uninstall") ? $"tool {command} WildernessLabs.Meadow.CLI --global" : $"tool {command} WildernessLabs.Meadow.CLI --global --version {version}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            var proc = Process.Start(psi);
            _ = proc?.WaitForExit(milliSecondWait);
            return proc;
        }
    }
}