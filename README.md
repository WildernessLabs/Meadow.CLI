# MeadowCLI

## Getting Started

Note: For OSX users, [this line](https://github.com/WildernessLabs/MeadowCLI/blob/master/MeadowCLI/DfuSharp.cs#L29) needs to be changed to `libusb-1.0` TODO: determine OS at runtime or handle fallback

The CLI tool supports DFU flashing for `nuttx.bin` and `nuttx_user.bin`. When the application is run with `-d`, it looks for `nuttx.bin` and `nuttx_user.bin` in the application directory and if not found, it will abort. Optionally, paths for the files can be specific with `--osFile` and `--userFile`.

The CLI tool also supports device and file management including file transfers, flash partitioning, and MCU reset.

To run MeadowCLI on Windows, run meadow.exe from the command prompt. On Mac and Windows, call **mono meadow.exe**.

## Options

To see the options, run the application with the --help arg.

## Running Commands

File and device commands require you to specify the serial port. You can determine the serial port name in Windows by viewing the Device Manager. The CLI will remember the last Serial Port used, so you only need to specify if the value has changed.

On Mac and Linux, the serial port will show up in the **/dev** folder, generally with the prefix **tty.usb**. You can likely find the serial port name by running the command `ls /dev/tty.usb`.

## Useful commands

### Update the Meadow OS
```
meadow flash os
```

### Set the trace level

You can set the debug trace level to values 0, 1, 2, or 3. 2 is the most useful.
```
meadow trace enable --level 2 -s [NameOfSerialPort]
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
meadow files delete -f
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
meadow debug --port XXXX
```
This starts listening on the specified port for a debugger to attach

Note: you can use SDB command line debugger from https://github.com/mono/sdb. Just build it according to its readme, run the above command and then:

`sdb "connect 127.0.0.1 XXXX"` (substitute XXXX for the same port number as above)

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
