﻿using System;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine.Commands.DeviceManagement
{
    [Command("device info", Description = "Get the device info")]
    public class GetDeviceInfoCommand : MeadowSerialCommand
    {
        private readonly ILogger<GetDeviceInfoCommand> _logger;

        public GetDeviceInfoCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<GetDeviceInfoCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device =
                await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, true, cancellationToken).ConfigureAwait(false);

            var deviceInfoString = await device.GetDeviceInfo(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(deviceInfoString))
                throw new Exception("Unable to retrieve device info");
            var deviceInfo = new MeadowDeviceInfo(deviceInfoString);
            _logger.LogInformation(deviceInfo.ToString());
        }
    }
}
