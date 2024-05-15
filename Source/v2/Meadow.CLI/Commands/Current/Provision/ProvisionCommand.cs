using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Management;
using CliFx.Attributes;
using LibUsbDotNet.Info;
using LibUsbDotNet.LibUsb;
using Meadow.CLI.Commands.DeviceManagement;
using Meadow.CLI.Core.Internals.Dfu;
using Meadow.LibUsb;
using Meadow.Software;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Meadow.CLI.Commands.Provision;

[Command("provision", Description = "Provision 1 or more devices that are in DFU mode.")]
public class ProvisionCommand : BaseDeviceCommand<ProvisionCommand>
{
    public const string DefaultOSVersion = "1.11.0.0";
    [CommandOption("version", 'v', Description = "Target OS version for devices to be provisioned with", IsRequired = false)]
    public string? OsVersion { get; set; } = DefaultOSVersion;
    private string? osVersion = string.Empty;

    private ObservableConcurrentQueue<BootLoaderDevice> bootloaderDeviceQueue = new ObservableConcurrentQueue<BootLoaderDevice>();
    //private ObservableConcurrentQueue<BootLoaderDevice> processingDeviceQueue = new ObservableConcurrentQueue<BootLoaderDevice>();

    private FileManager FileManager { get; }
    private ISettingsManager Settings { get; }
    private MeadowConnectionManager MeadowConnectionManager { get; }

    public ProvisionCommand(ISettingsManager settingsManager, FileManager fileManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        FileManager = fileManager;
        Settings = settingsManager;
        MeadowConnectionManager = connectionManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        AnsiConsole.MarkupLine("Provisioning");

        if (string.IsNullOrEmpty(OsVersion))
            osVersion = DefaultOSVersion;
        else
            osVersion = OsVersion;

        // Install DFU, if it's not already installed.
        var dfuInstallCommand = new DfuInstallCommand(Settings, LoggerFactory);
        await dfuInstallCommand.ExecuteAsync(Console);

        if (await MeadowCLI($"firmware download -v {osVersion} -f") == 0)
        {
            bool refreshDeviceList = false;
            List<BootLoaderDevice> selectedDevices;
            do
            {
                await UpdateDeviceList(CancellationToken);

                if (bootloaderDeviceQueue.Count == 0)
                {
                    Logger?.LogError($"No devices found in bootloader mode. Rerun this command when at least 1 connected device is in bootloader mode.");
                    return;
                }

                var multiSelectionPrompt = new MultiSelectionPrompt<BootLoaderDevice>()
                    .Title("Devices in Bootloader mode")
                    .PageSize(15)
                    .NotRequired() // Can be Blank to exit
                    .MoreChoicesText("More Choices")
                    .InstructionsText("Instructions")
                    .UseConverter(x => x.SerialPort);

                foreach (var device in bootloaderDeviceQueue)
                {
                    multiSelectionPrompt.AddChoices(device);
                }

                selectedDevices = AnsiConsole.Prompt(multiSelectionPrompt);

                var selectedDeviceTable = new Table();
                selectedDeviceTable.AddColumn("Selected Devices");

                foreach (var device in selectedDevices)
                {
                    selectedDeviceTable.AddRow(device.SerialPort);
                }

                AnsiConsole.Write(selectedDeviceTable);

               refreshDeviceList = AnsiConsole.Confirm(Strings.ProvisionRefreshDeviceList);
            } while (refreshDeviceList);

            
            if (selectedDevices.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No devices selected to update[/]. Exiting.");
                return;
            }
            else
            {
                foreach (var item in selectedDevices)
                {
                    await AnsiConsole.Status()
                        .Start("Thinking...", async ctx =>
                        {
                            AnsiConsole.MarkupLine($"Flashing [green]{item.SerialPort}[/]");

                            if (await MeadowCLI($"firmware write -v {osVersion} -s {item.SerialNumber}") == 0)
                            {
                            }
                            else
                            {
                                Logger?.LogError($"Error flash in {item.SerialPort} :(");
                            }
                        });
                    
                }
            }
        }
        else
        {
            Logger?.LogError($"Unable to download os v{OsVersion}. Please check your internet conneciton.");
        }
    }

    private void FirmwareWriteCommand_FlashProgress(object? sender, FirmwareType e)
    {
        Logger?.LogInformation($"Writing {e}");
    }

    private async Task UpdateDeviceList(CancellationToken token)
    {
        var ourDevices = await GetValidUsbDevices();

        if (ourDevices?.Count() > 0)
        {
            bootloaderDeviceQueue.Clear();

            foreach (ILibUsbDevice device in ourDevices)
            {
                if (bootloaderDeviceQueue != null)
                {
                    if (device != null)
                    {
                        var deviceSerialNumber = device.GetDeviceSerialNumber();

                        var serialPort = (Environment.OSVersion.Platform == PlatformID.Unix
                            || Environment.OSVersion.Platform == PlatformID.MacOSX)
                            ? $"/dev/tty.usbmodem{deviceSerialNumber}1"
                            : $"COM{await GetComPortNumber()}";

                        // Does the serial number match
                        var serialNumberMatch = (bootloaderDeviceQueue.Count(d => d.SerialNumber == deviceSerialNumber) > 0);
                        if (serialNumberMatch || string.IsNullOrEmpty(deviceSerialNumber))
                        {
                            continue;
                        }

                        bootloaderDeviceQueue.Enqueue(new BootLoaderDevice
                        {
                            DeviceObject = device,
                            SerialNumber = deviceSerialNumber,
                            SerialPort = serialPort,
                        });
                    }
                }
            }
        }
    }

    private async Task<int> GetComPortNumber()
    {
        return await Task.Run(() =>
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE 'USB\\VID_" /* TODO + deviceInfo.VendorId.ToString("X4")*/ + "&PID_" /*+ deviceInfo.ProductId.ToString("X4")*/ + "%'"))
            {
                foreach (ManagementObject mo in searcher.Get())
                {
                    string deviceId = (string)mo["DeviceID"];
                    string portName = deviceId.Substring(deviceId.LastIndexOf("_") + 1);
                    return int.Parse(portName.Replace("#", ""));
                }
            }
            throw new Exception("COM port not found");
        });
    }

    private async Task<IEnumerable<ILibUsbDevice>>? GetValidUsbDevices()
    {
        try
        {
            return await Task.Run(() =>
            {
                var provider = new LibUsbProvider();

                var devices = provider.GetDevicesInBootloaderMode();

                return devices;
            });
        }
        catch (Exception)
        {
            //footerMessage = ex.Message;
            return null;
        }
    }

    static async Task<int> MeadowCLI(string arg, bool redirectStandardOutput = true, bool redirectStandardError = true, bool redirectStandardInput = false)
    {
        using (var process = new Process())
        {
            process.StartInfo.FileName = "./meadow";
            process.StartInfo.Arguments = $"{arg}";
            process.StartInfo.WorkingDirectory = System.AppContext.BaseDirectory;

            process.StartInfo.UseShellExecute = false;

            process.StartInfo.CreateNoWindow = true;

            process.StartInfo.RedirectStandardOutput = redirectStandardOutput;
            process.StartInfo.RedirectStandardError = redirectStandardError;
            process.StartInfo.RedirectStandardInput = redirectStandardInput;

            process.Start();

            await process.WaitForExitAsync();

            return process.ExitCode;
        }
    }
}

internal class BootLoaderDevice
{
    public ILibUsbDevice? DeviceObject { get; internal set; }
    internal string SerialPort { get; set; } = string.Empty;
    internal string SerialNumber { get; set; } = string.Empty;
    internal string CurrentStatus { get; set; } = string.Empty;
}

