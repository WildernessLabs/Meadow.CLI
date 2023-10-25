﻿using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Meadow.Hcom;

public class LocalConnection : ConnectionBase
{
    public override string Name => "Local";

    private DeviceInfo? _deviceInfo;

    public LocalConnection()
    {
    }

    public override Task<IMeadowDevice?> Attach(CancellationToken? cancellationToken = null, int timeoutSeconds = 10)
    {
        Device = new MeadowDevice(this);
        return Task.FromResult<IMeadowDevice?>(Device);
    }

    public override Task<DeviceInfo?> GetDeviceInfo(CancellationToken? cancellationToken = null)
    {
        if (_deviceInfo == null)
        {
            var info = new Dictionary<string, string>
            {
                { "Model", $"{Environment.OSVersion.Platform} {Environment.OSVersion.Version.ToString(2)}" },
                { "Product", "Local Device" }
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                info.Add("DeviceName", Environment.MachineName);
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0\"))
                {
                    info.Add("ProcessorType", (key?.GetValue("ProcessorNameString")?.ToString() ?? "Unknown").Trim());
                }
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography\"))
                {
                    info.Add("SerialNo", (key?.GetValue("MachineGuid")?.ToString() ?? "Unknown").Trim().ToUpper());
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                info.Add("DeviceName", Environment.MachineName);
                info.Add("SerialNo", File.ReadAllText("/var/lib/dbus/machine-id").Trim());
            }

            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                info.Add("DeviceName", Environment.MachineName);

                var mac_id = ExecuteBashCommandLine("ioreg -l | grep IOPlatformSerialNumber | sed 's/.*= //' | sed 's/\\\"//g'");
                info.Add("SerialNo", mac_id.Trim());
            }
            _deviceInfo = new DeviceInfo(info);
        }

        return Task.FromResult<DeviceInfo?>(_deviceInfo);
    }

    private string ExecuteBashCommandLine(string command)
    {
        var psi = new ProcessStartInfo()
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);

        process?.WaitForExit();

        return process?.StandardOutput.ReadToEnd() ?? string.Empty;
    }

    private string ExecuteWindowsCommandLine(string command, string args)
    {
        var psi = new ProcessStartInfo()
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);

        process?.WaitForExit();

        return process?.StandardOutput.ReadToEnd() ?? string.Empty;
    }

    public override Task<string> GetPublicKey(CancellationToken? cancellationToken = null)
    {
        // DEV NOTE: this *must* be in PEM format: i.e. -----BEGIN RSA PUBLIC KEY----- ... -----END RSA PUBLIC KEY---- 

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var sshFolder = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh"));

            if (!sshFolder.Exists)
            {
                throw new Exception("SSH folder not found");
            }
            else
            {
                var pkFile = Path.Combine(sshFolder.FullName, "id_rsa.pub");
                if (!File.Exists(pkFile))
                {
                    throw new Exception("Public key not found");
                }

                var pkFileContent = File.ReadAllText(pkFile);
                if (!pkFileContent.Contains("BEGIN RSA PUBLIC KEY", StringComparison.OrdinalIgnoreCase))
                {
                    // need to convert
                    pkFileContent = ExecuteWindowsCommandLine("ssh-keygen", $"-e -m pem -f {pkFile}");
                }

                return Task.FromResult(pkFileContent);
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // this will generate a PEM output *assuming* the key has already been created
            var keygenOutput = ExecuteBashCommandLine("ssh-keygen -f id_rsa -e -m pem");
            if (!keygenOutput.Contains("BEGIN RSA PUBLIC KEY", StringComparison.OrdinalIgnoreCase))
            {
                // probably no key generated
                throw new Exception("Unable to retrieve a public key.  Please run 'ssh-keygen -t rsa'");
            }

            return Task.FromResult(keygenOutput);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // ssh-agent sh -c 'ssh-add; ssh-add -L'
            var pubkey = this.ExecuteBashCommandLine("ssh-agent sh -c 'ssh-add; ssh-add -L'");
            if (!pubkey.Contains("BEGIN RSA PUBLIC KEY", StringComparison.OrdinalIgnoreCase))
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "id_rsa.pub");
                pubkey = ExecuteBashCommandLine($"ssh-keygen -f {path} -e -m pem");
            }
            return Task.FromResult(pubkey);
        }
        else
        {
            throw new PlatformNotSupportedException();
        }
    }





    public override Task DeleteFile(string meadowFileName, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task EraseFlash(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<MeadowFileInfo[]?> GetFileList(bool includeCrcs, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<DateTimeOffset?> GetRtcTime(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<bool> IsRuntimeEnabled(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<bool> ReadFile(string meadowFileName, string? localFileName = null, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<string?> ReadFileString(string fileName, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task ResetDevice(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task RuntimeDisable(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task RuntimeEnable(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task SetDeveloperParameter(ushort parameter, uint value, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task SetRtcTime(DateTimeOffset dateTime, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task SetTraceLevel(int level, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task StartDebugging(int port, ILogger? logger, CancellationToken? cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override Task<DebuggingServer> StartDebuggingSession(int port, ILogger? logger, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override Task TraceDisable(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task TraceEnable(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task UartTraceDisable(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task UartTraceEnable(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task WaitForMeadowAttach(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<bool> WriteCoprocessorFile(string localFileName, int destinationAddress, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<bool> WriteFile(string localFileName, string? meadowFileName = null, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<bool> WriteRuntime(string localFileName, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }
}
