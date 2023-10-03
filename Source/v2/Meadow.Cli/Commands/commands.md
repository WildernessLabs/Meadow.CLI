# Commands

| v1 | v2 | Legacy Implemented |
|---|---|---|---|
| list ports | port list | n |
| n/a | port select | - |
| listen | listen | - |
| device info | device info | - |
| n/a | device reset | - |
| n/a | device clock | - |
| use port | config route | n |
| mono enable | runtime enable | y |
| mono disable | runtime disable | y |
| mono state | runtime state | n |
| file list | file list | - |
| file delete | file delete | - |
| n/a | file read | - |
| file write | file write | - |
| file initial | file initial | - |
| n/a | firmware list | - |
| download os | firmware download | n |
| n/a | firmware default | - |
| n/a | firmware delete | - |
| flash esp | firmware write esp | n |
| flash os | firmware write os | n |
| mono flash | flash write runtime | n |
| mono update rt | flash write runtime | n |
| trace enable | trace enable | - |
| trace disable | trace disable | - |
| trace level | trace level | - |
| set developer | developer |
| uart trace | uart trace | - |
| n/a | app build | - |
| n/a | app trim | - |
| install dfu-util | dfu install | n |
| app deploy | app deploy | - |
| n/a | app run | - |
| flash erase | flash erase | - |
| debug | TODO |
| device provision | device provision | - |
| package create | cloud package create | n |
| package list | cloud package list |
| package publish | cloud package publish |
| package upload | cloud package upload |
| collection list | cloud collection list | - |
| cloud login | cloud login | - |
| cloud logout | cloud logout | - |
| cloud command publish | cloud command publish | - |




Legacy List (are any of these still needed?)
  device mac        Read the ESP32's MAC address
  device name       Get the name of the Meadow
  esp32 file write  Write files to the ESP File System
  esp32 restart     Restart the ESP32
  flash verify      Verify the contents of the flash were deleted
  fs renew          Create a File System on the Meadow Board
  nsh disable       Disables NSH on the Meadow device
  nsh enable        Enables NSH on the Meadow device
  qspi init         Init the QSPI on the Meadow
  qspi read         Read a QSPI value from the Meadow
  qspi write        Write a QSPI value to the Meadow
