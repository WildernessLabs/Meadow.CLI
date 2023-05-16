<img alt="Meadow CLI project banner stating Meadow's Command-Line-Interface to interact with the board and perform functions via a terminal/command-line window." src="Design/banner.jpg" style="margin-bottom:10px" />

## Build Status
[![Build](https://github.com/WildernessLabs/Meadow.CLI/actions/workflows/dotnet.yml/badge.svg)](https://github.com/WildernessLabs/Meadow.CLI/actions)

## Getting Started

The CLI tool supports DFU flashing for `nuttx.bin` and `nuttx_user.bin`. When the application is run with `-d`, it looks for `nuttx.bin` and `nuttx_user.bin` in the application directory and if not found, it will abort. Optionally, paths for the files can be specific with `--osFile` and `--userFile`.

The CLI tool also supports device and file management including file transfers, flash partitioning, and MCU reset.

To run Meadow.CLI on Windows, run meadow.exe from the command prompt. On Mac and Windows, call **mono meadow.exe**.

## Options

To see the options, run the application with the --help arg.

## Running Commands

### Specifying the Serial Port
File and device commands require you to specify the serial port (`-s` or `--SerialPort`). You can determine the serial port name in Windows by viewing the Device Manager. The CLI will remember the last Serial Port used, so you only need to specify it if you need to change the value.

On Mac and Linux, the serial port will show up in the **/dev** folder, generally with the prefix **tty.usb**. You can likely find the serial port name by running the command `ls /dev/tty.usb`.

### Setting the Log Verbosity
Appending `-v` or `-vv` to any command will increase the logging verbosity to `Debug` and `Trace` respectively. `Trace` should only be necessary when debugging issues with the CLI.

### Available Commands

```console
meadow v1.0.0

USAGE
  meadow [options]
  meadow [command] [...]

OPTIONS
  -h|--help         Shows help text. 
  --version         Shows version information. 

COMMANDS
  app deploy        Deploy the specified app to the Meadow 
  cloud login       Log into the Meadow Service 
  cloud logout      Logout of the Meadow Service 
  debug             Debug a Meadow Application 
  device info       Get the device info 
  device mac        Read the ESP32's MAC address 
  device name       Get the name of the Meadow 
  device provision  Registers and prepares connected device for use with Meadow Cloud 
  download os       Downloads the latest Meadow.OS to the host PC 
  esp32 file write  Write files to the ESP File System 
  esp32 restart     Restart the ESP32 
  file delete       Delete files from the Meadow File System Subcommands: file delete all.
  file initial      Get the initial bytes from a file 
  file list         List files in the on-board filesystem 
  file write        Write files to the Meadow File System 
  flash erase       Erase the flash on the Meadow Board 
  flash esp         Flash the ESP co-processor 
  flash os          Update the OS on the Meadow Board 
  flash verify      Verify the contents of the flash were deleted 
  fs renew          Create a File System on the Meadow Board 
  install dfu-util  Install the DfuUtil utility 
  list ports        List available COM ports 
  listen            Listen for console output from Meadow 
  mono disable      Sets mono to NOT run on the Meadow board then resets it 
  mono enable       Sets mono to run on the Meadow board and then resets it 
  mono flash        Uploads the mono runtime file to the Meadow device. Does NOT move it into place 
  mono state        Returns whether or not mono is enabled or disabled on the Meadow device 
  mono update rt    Uploads the mono runtime files to the Meadow device and moves it into place 
  nsh disable       Disables NSH on the Meadow device 
  nsh enable        Enables NSH on the Meadow device 
  package create    Create Meadow Package 
  package list      List Meadow Packages 
  package publish   List Meadow Packages 
  package upload    Upload Meadow Package 
  qspi init         Init the QSPI on the Meadow 
  qspi read         Read a QSPI value from the Meadow 
  qspi write        Write a QSPI value to the Meadow 
  set developer     Set developer value 
  trace disable     Disable Trace Logging on the Meadow 
  trace enable      Enable trace logging on the Meadow 
  trace level       Enable trace logging on the Meadow 
  uart trace        Configure trace logs to go to UART 
  use port          Set the preferred serial port
```

### Getting Help

Specifying `--help` with no command will output the list of available commands. Specifying `--help` after a command (e.g., `meadow file delete --help`) will output command specific help.

```console
meadow v1.0.0

USAGE
  meadow file delete --files <values...> [options]
  meadow file delete [command] [...]

DESCRIPTION
  Delete files from the Meadow File System

OPTIONS
* -f|--files        The file(s) to delete from the Meadow Files System
  -s|--SerialPort   Meadow COM port Default: "COM10".
  -g|--LogVerbosity  Log verbosity
  -h|--help         Shows help text.

COMMANDS
  all               Delete all files from the Meadow File System

You can run `meadow file delete [command] --help` to show help on a specific command.
Done!
```

## Useful commands

### Update the Meadow OS
```
meadow flash os
```

#### Meadow.CLI download location

If you need to find or clear out any of the OS download files retrieved by Meadow.CLI, they are located in a WildernessLabs folder in the user directory.

macOS: `~/.local/share/WildernessLabs/Firmware/`
Windows: `%LOCALAPPDATA%\WildernessLabs\Firmware`

### Listen for Meadow Console.WriteLine
```
meadow listen
```

### Set the trace level

You can set the debug trace level to values 0, 1, 2, or 3. 2 is the most useful.
```
meadow trace enable --level 2
```

### File transfers
```
meadow files write -f [NameOfFile]
```
You may specify multiple instances of `-f` to send multiple files

### List files in flash
```
meadow files list
```

### Delete a File

```
meadow files delete -f [NameOfFile]
```
You may specify multiple instances of `-f` to send multiple files

### Stop/start the installed application from running automatically
```
meadow mono disable
meadow mono enable
```
### Useful utilities
```
meadow device info
meadow device name
```

### Debugging
**NOTE THIS IS NOT YET FULLY IMPLEMENTED, IT WILL NOT WORK**
```
meadow debug --DebugPort XXXX
```
This starts listening on the specified port for a debugger to attach

Note: you can use SDB command line debugger from https://github.com/mono/sdb. Just build it according to its readme, run the above command and then:

```
sdb "connect 127.0.0.1 XXXX"
``` 
Substitute XXXX for the same port number as above

## Running applications

You'll typically need at least 5 files installed to the Meadow flash to run a Meadow app:

1. System.dll
2. System.Core.dll
3. mscorlib.dll
4. Meadow.Core.dll
5. App.exe (your app)

It's a good idea to disable mono first, copy the files, and then enable mono

## Uninstall the Meadow.CLI tool

If you ever need to remove the Meadow.CLI tool, you can remove it through the .NET command-line tool as you would any other global tool.

```console
dotnet tool uninstall WildernessLabs.Meadow.CLI --global
```

## Install a downloaded pre-release version

If you want to test one of the automated pre-release builds of the Meadow.CLI tool you have downloaded, you'll need to specific some extra parameters.

1. Download a pre-release version, typically from an [automated build](https://github.com/WildernessLabs/Meadow.CLI/actions).
1. Extract the package .nupkg file from the downloaded archive.
1. Uninstall the existing tool.

    ```console
    dotnet tool uninstall WildernessLabs.Meadow.CLI --global
    ```

1. Install the pre-release version from the download location by providing a version parameter for `{pre-release-version}` and source location of the .nupkg file for `{path-to-folder-with-downloaded-nupkg}`.

    ```console
    dotnet tool install WildernessLabs.Meadow.CLI --version '{pre-release-version}' --global --add-source '{path-to-folder-with-downloaded-nupkg}'
    ```

1. Verify the version of your Meadow.CLI tool.

    ```console
    meadow --version
    ```

### Return to an official release version

After you are done testing a pre-release build, you can return to the official Meadow.CLI release by uninstalling and reinstalling without the local overrides.

```console
dotnet tool uninstall WildernessLabs.Meadow.CLI --global
dotnet tool install WildernessLabs.Meadow.CLI --global
```

# License

Copyright Wilderness Labs Inc.
    
    Licensed under the Apache License, Version 2.0 (the "License");
    you may not use this file except in compliance with the License.
    You may obtain a copy of the License at
    
      http://www.apache.org/licenses/LICENSE-2.0
    
    Unless required by applicable law or agreed to in writing, software
    distributed under the License is distributed on an "AS IS" BASIS,
    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    See the License for the specific language governing permissions and
    limitations under the License.
