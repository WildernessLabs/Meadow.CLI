using System;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands
{
    public abstract class MeadowSerialCommand : ICommand
    {
        private protected ILoggerFactory LoggerFactory;
        private protected MeadowDeviceManager MeadowDeviceManager;
        private protected string SettingsFilePath;

        private protected MeadowSerialCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
        {
            LoggerFactory = loggerFactory;
            MeadowDeviceManager = meadowDeviceManager;
        }

        [CommandOption('v', Description = "Log verbosity")]
        public string[] Verbosity { get; init; }

        [CommandOption("port", 's', Description = "Meadow COM port")]
        public string SerialPortName
        {
            get => GetSerialPort();
            set => SetSerialPort(value);
        }

        [CommandOption("listen", 'k', Description = "Keep port open to listen for output")]
        public bool Listen {get; init;}

        private string _serialPort;

        private string GetSerialPort()
        {
            if (string.IsNullOrWhiteSpace(_serialPort))
            {
                // TODO: VALIDATE THE INPUT HERE, INPUT IS UNVALIDATED
                var port = SettingsManager.GetSetting(Setting.PORT);

                SerialPortName = port.Trim();
            }

            return _serialPort;
        }

        private void SetSerialPort(string value)
        {
            _serialPort = value;
            SettingsManager.SaveSetting(Setting.PORT, _serialPort);
        }

        public abstract ValueTask ExecuteAsync(IConsole console);
    }
}
