using CliFx.Infrastructure;
using Meadow.CLI.Commands.DeviceManagement;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Meadow.HCom.Integration.Tests
{
    public class CliTests
    {
        [Fact]
        public async Task ConfigTest()
        {
            var factory = new LoggerFactory().AddSerilog(Log.Logger);

            using var console = new FakeInMemoryConsole();

            var listCommand = new ConfigCommand(new InMemorySettingsManager(), factory)
            {
                List = true
            };

            var setCommand = new ConfigCommand(new InMemorySettingsManager(), factory)
            {
                Settings = new string[] { "route", "COM8" }
            };

            await setCommand.ExecuteAsync(console);

            var stdOut = console.ReadOutputString();

            // Act
            await listCommand.ExecuteAsync(console);

            // Assert
            stdOut = console.ReadOutputString();
            //            Assert.That(stdOut, Is.EqualTo("foo bar"));
        }
    }
}