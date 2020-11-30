using CommandLine;

namespace MeadowCLI
{
    public class Options
    {
        [Option('d', "Dfu", Required = false, HelpText = "DFU copy os and user files. Looks for files in execution direction. To override, user 'OsFile' and 'UserFile'.")]
        public bool Dfu { get; set; }
        [Option(longName: "OsFile", Default = null, Required = false, HelpText = "File path to os file. Usage: --OsFile mypath")]
        public string DfuOsPath { get; set; }
        [Option(longName: "UserFile", Default = null, Required = false, HelpText = "File path to user file. Usage: --UserFile mypath")]
        public string DfuUserPath { get; set; }

        [Option(longName: "WriteFile", Required = false, HelpText = "Write an external file to Meadow's internal flash")]
        public bool WriteFile { get; set; }
        [Option(longName: "DeleteFile", Required = false, HelpText = "Delete a file in Meadow's internal flash")]
        public bool DeleteFile { get; set; }
        [Option(longName: "EraseFlash", Required = false, HelpText = "Delete all content in Meadow flash")]
        public bool EraseFlash { get; set; }
        [Option(longName: "VerifyErasedFlash", Required = false, HelpText = "Verify the contents of the flash were deleted")]
        public bool VerifyErasedFlash { get; set; }
        [Option(longName: "PartitionFileSystem", Required = false, HelpText = "Partition Meadow's internal flash")]
        public bool PartitionFileSystem { get; set; }
        [Option(longName: "MountFileSystem", Required = false, HelpText = "Mount file system in Meadow's internal flash")]
        public bool MountFileSystem { get; set; }
        [Option(longName: "InitializeFileSystem", Required = false, HelpText = "Initialize file system in Meadow's internal flash")]
        public bool InitFileSystem { get; set; }
        [Option(longName: "CreateFileSystem", Required = false, HelpText = "Create a new file system in Meadow's internal flash")]
        public bool CreateFileSystem { get; set; }
        [Option(longName: "FormatFileSystem", Required = false, HelpText = "Format file system in Meadow's internal flash")]
        public bool FormatFileSystem { get; set; }
        [Option(longName: "ClearCache", Required = false, HelpText = "Clears the CLI's state cache")]
        public bool ClearCache { get; set; }
        
        [Option(longName: "SetDeveloper1", Required = false, HelpText = "Set developer1 (0 to 4,294,967,295)")]
        public bool SetDeveloper1 { get; set; }
        [Option(longName: "SetDeveloper2", Required = false, HelpText = "Set developer2 (0 to 4,294,967,295)")]
        public bool SetDeveloper2 { get; set; }
        [Option(longName: "SetDeveloper3", Required = false, HelpText = "Set developer3 (0 to 4,294,967,295)")]
        public bool SetDeveloper3 { get; set; }
        [Option(longName: "SetDeveloper4", Required = false, HelpText = "Set developer4 (0 to 4,294,967,295)")]
        public bool SetDeveloper4 { get; set; }
        [Option(longName: "SetTraceLevel", Required = false, HelpText = "Change the debug trace level (0 - 3)")]
        public bool SetTraceLevel { get; set; }
        [Option('r', longName: "ResetMeadow", Required = false, HelpText = "Reset the MCU on Meadow")]
        public bool ResetMeadow { get; set; }
        [Option(longName: "EnterDfuMode", Required = false, HelpText = "Put Meadow in DFU mode - Not implemented")]
        public bool EnterDfuMode { get; set; }
        [Option(longName: "NshEnable", Required = false, HelpText = "Enable NSH")]
        public bool NshEnable { get; set; }
        [Option(longName: "MonoDisable", Required = false, HelpText = "Disable mono from running")]
        public bool MonoDisable { get; set; }
        [Option(longName: "MonoEnable", Required = false, HelpText = "Enable mono so it can run")]
        public bool MonoEnable { get; set; }
        [Option(longName: "MonoRunState", Required = false, HelpText = "Reads mono startup state")]
        public bool MonoRunState { get; set; }
        [Option(longName: "MonoFlash", Required = false, HelpText = "Flashes Mono runtime to the external flash")]
        public bool MonoFlash { get; set; }
        [Option(longName: "MonoUpdateRt", Required = false, HelpText = "Download runtime files and flashes Mono runtime to flash")]
        public bool MonoUpdateRt { get; set; }
        [Option(longName: "GetDeviceInfo", Required = false, HelpText = "Reads device information")]
        public bool GetDeviceInfo { get; set; }
        [Option(longName: "GetDeviceName", Required = false, HelpText = "Reads device name")]
        public bool GetDeviceName { get; set; }

        [Option(longName: "ListFiles", Required = false, HelpText = "List all files in Meadow partition")]
        public bool ListFiles { get; set; }
        [Option(longName: "ListFilesAndCrcs", Required = false, HelpText = "List all files and CRCs in a Meadow partition")]
        public bool ListFilesAndCrcs { get; set; }
        
        [Option(longName: "ListPorts", Required = false, HelpText = "List all available local serial ports")]
        public bool ListPorts { get; set; }
        [Option('s', longName: "SerialPort", Required = false, HelpText = "Specify the serial port used by Meadow")]
        public string SerialPort { get; set; }
        [Option('f', longName: "File", Default = null, Required = false, HelpText = "Local file to send to Meadow")]
        public string FileName { get; set; }
        [Option(longName: "TargetFileName", Default = null, Required = false, HelpText = "Filename to be written to Meadow (can be different from source name")]
        public string TargetFileName { get; set; }
        [Option('p', "Partition", Default = 0, Required = false, HelpText = "Destination partition on Meadow")]
        public int Partition { get; set; }
        [Option('n', "NumberOfPartitions", Default = 1, Required = false, HelpText = "The number of partitions to create on Meadow")]
        public int NumberOfPartitions { get; set; }
        [Option('t', "TraceLevel", Default = 1, Required = false, HelpText = "Change the amount of debug information provided by the OS")]
        public int TraceLevel { get; set; }
        [Option(longName: "DeveloperValue", Default = 0, Required = false, HelpText = "Change the developer numeric user data value")]
        public int DeveloperValue { get; set; }

        [Option(longName: "RenewFileSys", Required = false, HelpText = "Recreate the Meadow File System")]
        public bool RenewFileSys { get; set; }
        [Option(longName: "KeepAlive", Required = false, HelpText = "Keeps MeadowCLI from terminating after sending")]
        public bool KeepAlive { get; set; }
        [Option(longName: "TraceDisable", Required = false, HelpText = "Prevent Meadow from sending internal trace messages (default)")]
        public bool TraceDisable { get; set; }
        [Option(longName: "TraceEnable", Required = false, HelpText = "Request Meadow to send internal trace messages")]
        public bool TraceEnable { get; set; }
        [Option(longName: "Uart1Apps", Required = false, HelpText = "Use Uart1 for .NET apps")]
        public bool Uart1Apps { get; set; }
        [Option(longName: "Uart1Trace", Required = false, HelpText = "Use Uart1 for outputting Meadow trace messages")]
        public bool Uart1Trace { get; set; }
        [Option(longName: "QspiWrite", Required = false, HelpText = "Set developer1 (0 to 4,294,967,295)")]
        public bool QspiWrite { get; set; }
        [Option(longName: "QspiRead", Required = false, HelpText = "Set developer2 (0 to 4,294,967,295)")]
        public bool QspiRead { get; set; }
        [Option(longName: "QspiInit", Required = false, HelpText = "Set developer3 (0 to 4,294,967,295)")]
        public bool QspiInit { get; set; }

        [Option(longName: "StartDebugging", Required = false, HelpText = "Enables remote debug of Meadow app.exe")]
        public bool StartDebugging { get; set; }
        [Option(longName: "VSDebugPort", Default = 0, Required = false, HelpText = "TCP/IP debugging port, Visual Studio 2019 uses 4024")]
        public int VSDebugPort { get; set; }

        [Option(longName: "Esp32WriteFile", Required = false, HelpText = "Write an external file to ESP32's internal flash")]
        public bool Esp32WriteFile { get; set; }
        [Option(longName: "McuDestAddr", Required = false, HelpText = "Where file is stored in MCU's internal flash e.g. 0x10000")]
        public string McuDestAddr { get; set; }
        [Option(longName: "Esp32ReadMac", Required = false, HelpText = "Read the ESP32's MAC address")]
        public bool Esp32ReadMac { get; set; }
        [Option(longName: "Esp32Restart", Required = false, HelpText = "Restart the ESP32")]
        public bool Esp32Restart { get; set; }
        [Option(longName: "DeployApp", Required = false, HelpText = "Deploy app and dependencies")]
        public bool DeployApp { get; set; }
    }
}