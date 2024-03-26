# MeadowCLI Commands

## Command Summary

This section contains an abbreviated list of necessary commands

Note: All commands require the name of the serial port. Once provided it will be cached by Meadow.CLI. While not shown below, the serial port format is `--SerialPort [NameOfSerialPort]` or `-s [NameOfSerialPort]`. Like all commands the serial port can appear anywhere within the command line.  

`Meadow.CLI --help arg`  
`Meadow.CLI --Dfu | -d --OsFile [Meadow.OS.bin]`  
`Meadow.CLI --ClearCache`  
`Meadow.CLI --ListPorts`  
`Meadow.CLI --KeepAlive` or `Meadow.CLI --AnyOtherCmd --KeepAlive`  

`Meadow.CLI --MonoDisable`  
`Meadow.CLI --MonoEnable`  
`Meadow.CLI --MonoRunState`  
`Meadow.CLI --MonoFlash`  
`Meadow.CLI --MonoUpdateRt`

`Meadow.CLI --WriteFile | -w file[,file,...] [targetFileName[,targetFileName,...]]`
`Meadow.CLI --DeleteFile | -d file[,file,...]`
`Meadow.CLI --ListFiles`  
`Meadow.CLI --ListFilesAndCrcs`  
`Meadow.CLI --RenewFileSys`  

`Meadow.CLI --GetDeviceInfo`  
`Meadow.CLI --GetDeviceName`  
`Meadow.CLI --ResetMeadow | -r`  
`Meadow.CLI --EraseFlash`  
`Meadow.CLI --VerifyErasedFlash`  

`Meadow.CLI --Esp32ReadMac`  
`Meadow.CLI --Esp32Restart`  
`Meadow.CLI --Esp32WriteFile --McuDestAddr [Address] --File [FilePathAndName]`  

`Meadow.CLI --SetTraceLevel | -t --TraceLevel 0, 1, 2, 3`  
`Meadow.CLI --TraceEnable`  
`Meadow.CLI --TraceDisable`  
`Meadow.CLI --Uart1Trace`  
`Meadow.CLI --Uart1Apps`  

`Meadow.CLI --StartDebugging`  

## Full Command Listing

This section contains a detailed list of all commands

### Meadow.CLI commands

`Meadow.CLI --help arg` - Outputs a list of all CLI commands, some of which are not be useful.  
`Meadow.CLI --ClearCache --SerialPort [NameOfSerialPort]` - Clears the cache holding saved serial port name  
`Meadow.CLI --ListPorts --SerialPort [NameOfSerialPort]` - Displays a list of available serial port  
`Meadow.CLI --Dfu | -d --OsFile [Meadow.OS.bin] --SerialPort [NameOfSerialPort]` - Adds or Update the Meadow.OS on the Meadow F7  
`EnterDfuMode` - *Not implemented in Meadow.OS*  
`UserFile` - *Obsolute* The original `nuttx_user.bin` file, is now named `Meadow.OS.Runtime.bin` and is written to Meadow file system using `WriteFile` and then moved to flash memory using the `MonoFlash` command.  
The `KeepAlive` command prevents MeadowCLI from terminating until the 'Enter' or 'return' key is pressed. This command can be use alone or in conjunction with other commands.  
`MeadowCLI.exe --KeepAlive --SerialPort [NameOfSerialPort]`  
`MeadowCLI.exe --ListFilesAndCrcs --KeepAlive --SerialPort [NameOfSerialPort]`

### Mono Related Commands

The Mono runtime can be prevented from running and re-enabled using the following 2 commands. Both commands automatically restart Meadow.  
`MeadowCLI.exe --MonoDisable --SerialPort [NameOfSerialPort]` - Disables Mono from running  
`MeadowCLI.exe --MonoEnable --SerialPort [NameOfSerialPort]` - Enables Mono to run  
`MeadowCLI.exe --MonoRunState --SerialPort [NameOfSerialPort]` - Reports if the Mono runtime will run after Meadow is restarted.  
`Meadow.CLI --MonoFlash --SerialPort [NameOfSerialPort] --KeepAlive` - copies the Meadow.OS.Runtime.bin file from the Meadow's file system to flash where is will be executed. Once this command is executed the Meadow.OS.Runtime.bin file can be deleted from the Meadow file system. Suggestion: use with the `--KeepAlive`  
`MeadowCLI.exe --MonoUpdateRt --SerialPort [NameOfSerialPort]` - Combines `--WriteFile` with `--MonoFlash` into a single command.  

### Meadow File System

`MeadowCLI.exe --WriteFile | -w file1[,file2,...] [file1TargetFileName[,file2TargetFileName,...]] --SerialPort [NameOfSerialPort]` - Writes a single file or multiple files to the Meadow file system. To write multiple files to the Meadow file system the files to be written are listed comma seperated. Optional it is possible to define the target file name in a comma seperated list for each file. 
`MeadowCLI --DeleteFile | -d file[,file,...] --SerialPort [NameOfSerialPort]` - Deletes one ore multiple files from the Meadow file system. To delete multiple files from the Meadow file system the files to be deleted are listed comma seperated. 
`MeadowCLI.exe --ListFiles --SerialPort [NameOfSerialPort]` -Lists all the files in the Meadow file system.
`MeadowCLI.exe --ListFilesAndCrcs --SerialPort [NameOfSerialPort] --KeepAlive` - List all the files in the Meadow file system and includes each files CRC checksum and size. This command can take several seconds for each file. Suggestion: use with the `--KeepAlive`  
`MeadowCLI.exe --RenewFileSys --SerialPort [NameOfSerialPort]` - Quickly recreate the Meadow file system.  

## Utility Commands

`MeadowCLI.exe --GetDeviceInfo --SerialPort [NameOfSerialPort]` - Outputs Meadow OS version and other device information.  
`MeadowCLI.exe --GetDeviceName --SerialPort [NameOfSerialPort]` - Reads the device name from an internal configuration file.  
`MeadowCLI.exe --ResetMeadow --SerialPort [NameOfSerialPort]` - Restarts the Meadow.OS  
`MeadowCLI.exe --EraseFlash --SerialPort [NameOfSerialPort] --KeepAlive` - Completely erase the Meadow external 32 MB flash.This includes the Meadow file system. Suggestion: use with the `--KeepAlive` since command takes signficant time to execute.  
`Meadow.CLI --VerifyErasedFlash --SerialPort [NameOfSerialPort] --KeepAlive` - Verifies that the Meadow's 32 MB flash has been completely erased. An erased flash's memory is one where all memory locations are set to 0xff. Suggestion: use with the `--KeepAlive` since command takes signficant time to execute.  

## ESP32 Commands

`Meadow.CLI --Esp32ReadMac --SerialPort [NameOfSerialPort]` - Reads the MAC Address of the ESP32 co-processor.  
`Meadow.CLI --Esp32Restart --SerialPort [NameOfSerialPort]` - Restarts the Esp32. The Esp32Restart command is required to restart the ESP32. memory. Restarting Meadow, even with the RST button, will not restart the ESP32. This command must be used after a file has been written to the ESP32's flash for the ESP32 to utilized the file.
`Meadow.CLI --Esp32WriteFile --SerialPort [NameOfSerialPort] --McuDestAddr [DestAddress] --File  [NameOfFile]` - Moves a file from the host PC/Mac into the ESP32's initernal memory.  
Multiple files can be combined in a CSV format as shown below.  
Example:
`Meadow.CLI --Esp32WriteFile --SerialPort [NameOfSerialPort] --File "0x8000, C:\WildernessLabs\ESP32Files\partition-table.bin, 0x1000, C:\WildernessLabs\ESP32Files\bootloader.bin, 0x10000, C:\WildernessLabs\ESP32Files\blink.bin"`
Note: There is no ESP32 file `delete` command because files in the ESP32 do not have names.  

## Diagnostic

You can set the debug trace level to values 0, 1, 2, or 3. The default is 0 which show only significant information and 3 provides a lot of information, trace level 2 is generally the most useful.  
`MeadowCLI.exe --SetTraceLevel --TraceLevel | -t 0, 1, 2, 3 --SerialPort [NameOfSerialPort]`
`Meadow.CLI --TraceEnable` - Routes Meadow OS trace information to Meadow.CLI (default)  
`Meadow.CLI --TraceDisable` - Stop routing Meadow OS trace information to Meadow.CLI  
`Meadow.CLI --Uart1Trace` - Routes Meadow OS trace information to COM1 (UART1) Tx=D12, RX=D13  
`Meadow.CLI --Uart1Apps` - Frees COM1 for Meadow application use (default)  
`Meadow.CLI --StartDebugging --VSDebugPort 4024` - Starts the remote debugging servers in Meadow.CLI and in Meadow.OS, allowing Visual Studio remote debugging of the .Net code running in Meadow. This command will also restart Meadow.  
Note: port 4024 is the default port for Visual Studio 2019

## Persisted Commands

The following commands are maintained by Meadow when the Meadow is restarted. However, no command is persisted if the Meadow is power cycled. After a power cycle the following default values will apply.  
`SetTraceLevel` - Default is a Trace Level of 0.  
`MonoEnable & MonoDisable` - Default is mono enable.  
`TraceEnable & TraceDisable` - Enables / disables trace to Meadow.CLI - Default is not to send trace to MeadowCLI.  
`Uart1Trace & Uart1App` -  Switches the function of Uart1 to/from the application from/to debugging. - Default is Uart1 available for application.
`StartDebugging` - Default is remote debugging disabled.  

## Extraneous Commands

Used only for Meadow.OS development and may not be implemented or work as expected in release versions of Meadow.OS.  
`Meadow.CLI --NshEnable --SerialPort Com26`  
`Meadow.CLI --SetDeveloper1-4 --DeveloperValue 1-65535`  
`Meadow.CLI --QspiWrite --SerialPort Com26 --DeveloperValue 65534`  
`Meadow.CLI --QspiRead --SerialPort Com26 --DeveloperValue 65534`  
`Meadow.CLI --QspiInit --SerialPort Com26 --DeveloperValue 65534`  

The following are not longer supported.
`Meadow.CLI --PartitionFileSystem`  
`Meadow.CLI --MountFileSystem`  
`Meadow.CLI --InitializeFileSystem`  
`Meadow.CLI --CreateFileSystem`  
`Meadow.CLI --FormatFileSystem`  
`Meadow.CLI --Partition`  
`Meadow.CLI --NumberOfPartitions`  

The following have not been implemented  
`Meadow.CLI --EnterDfuMode`  
