using System;
using System.Linq;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Devices;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands
{
    public abstract class MeadowSerialCommand : MeadowCommand, ICommand, IDisposable
    {
        private protected ILogger Logger;
        private protected MeadowDeviceManager MeadowDeviceManager;
        private protected MeadowDeviceHelper Meadow;

        private protected MeadowSerialCommand(DownloadManager downloadManager,
                                              ILoggerFactory loggerFactory,
                                              MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<MeadowSerialCommand>();
            MeadowDeviceManager = meadowDeviceManager;
        }

        [CommandOption("SerialPort", 's', Description = "Meadow COM port")]
        public string SerialPortName
        {
            get => GetSerialPort();
            set => SetSerialPort(value);
        }

        //[CommandOption("listen", 'k', Description = "Keep port open to listen for output")]
        //public bool Listen {get; init;}

        private string _serialPort;

        private string GetSerialPort()
        {
            if (string.IsNullOrWhiteSpace(_serialPort))
            {
                // TODO: VALIDATE THE INPUT HERE, INPUT IS UNVALIDATED
                var port = SettingsManager.GetSetting(Setting.PORT);
                if (!string.IsNullOrWhiteSpace(port))
                    SerialPortName = port.Trim();
            }

            return _serialPort;
        }

        private bool PortExists(string name)
        {
            return System.IO.Ports.SerialPort.GetPortNames().Contains(name);
        }

        private void SetSerialPort(string value)
        {
            _serialPort = value;
            SettingsManager.SaveSetting(Setting.PORT, _serialPort);
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            if(string.IsNullOrEmpty(SerialPortName))
            {
                LoggerFactory.CreateLogger<MeadowSerialCommand>().LogError("No serial port selected. Use 'meadow use port' to select a port");
                Environment.Exit(-2);
            }
            if(!PortExists(SerialPortName))
            {
                LoggerFactory.CreateLogger<MeadowSerialCommand>().LogError($"Selected serial port ({SerialPortName}) does not exist. Use 'meadow list ports' to view available options and 'meadow use port' to select a valid port");
                Environment.Exit(-2);
            }

            await base.ExecuteAsync(console);
            var meadow = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, logger: Logger).ConfigureAwait(false);
            if (meadow == null)
            {
                LoggerFactory.CreateLogger<MeadowSerialCommand>().LogCritical("Unable to find Meadow.");
                Environment.Exit(-1);
            }

            Meadow = new MeadowDeviceHelper(meadow, Logger);
        }

        public void Dispose()
        {
            LoggerFactory?.Dispose();
            Meadow?.Dispose();
        }
    }
}
