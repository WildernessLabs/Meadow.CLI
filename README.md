# MeadowCLI

## Getting Started

Note: For OSX users, [this line](https://github.com/WildernessLabs/MeadowCLI/blob/master/MeadowCLI/DfuSharp.cs#L29) needs to be changed to `libusb-1.0` TODO: determine OS at runtime or handle fallback

The CLI tool supports DFU flashing for `nuttx.bin` and `nuttx_user.bin`. When the application is run with `-d`, it looks for `nuttx.bin` and `nuttx_user.bin` in the application directory and if not found, it will abort. Optionally, paths for the files can be specific with `--osFile` and `--userFile`.

The CLI tool also supports device and file management including file transfers, flash partitioning, and MCU reset.

To run MeadowCLI on Windows, run MeadowCLI.exe from the command prompt. On Mac and Windows, call **mono MeadowCLI.exe**.

## Options

To see the options, run the application with the --help arg.

## Running Commands

File and device commands require you to specify the serial port. You can determine the serial port name in Windows by viewing the Device Manager.

On Mac and Linux, the serial port will show up in the **/dev** folder, generally with the prefix **tty.usb**. You can likely find the serial port name by running the command `ls /dev/tty.usb`.

## Useful commands

### Set the trace level

You can set the debug trace level to values 0, 1, 2, or 3. 2 is the most useful.
`MeadowCLI.exe --SetTraceLevel --Level 2 --SerialPort [NameOfSerialPort]`

### File transfers

`MeadowCLI.exe --WriteFile -f [NameOfFile] --SerialPort [NameOfSerialPort]`

### List files in flash

`MeadowCLI.exe --ListFiles --SerialPort [NameOfSerialPort]`

### Delete a File

`MeadowCLI.exe -s [NameOfSerialPort] --DeleteFile --TargetFileName [nameOfFile]`

### Reformat the flash

`MeadowCLI.exe --RenewFileSys` - Restarts Meadow and recreates file system

`MeadowCLI.exe --EraseFlash` - Must wait 2 - 3 minutes to complete

### Stop/start the installed application from running automatically

`MeadowCLI.exe --MonoDisable`

`MeadowCLI.exe --MonoEnable`

### Useful utilities

`MeadowCLI.exe --GetDeviceInfo` - Outputs Meadow OS version and other information
`MeadowCLI.exe --GetDeviceName` - Outputs Meadow device name contained in configuration file

## Running applications

You'll typically need at least 5 files installed to the Meadow flash to run a Meadow app:

1. System.dll
2. System.Core.dll
3. mscorlib.dll
4. Meadow.Core.dll
5. App.exe (your app)

It's a good idea to disable mono first, copy the files, and then enable mono


## Source Code Quality

This code is ugly. We know. :) Lots of different coding styles, spanning many decades and sensibilities.

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