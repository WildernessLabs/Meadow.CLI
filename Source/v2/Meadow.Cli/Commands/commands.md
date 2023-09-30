# Commands

| v1 | v2 | Legacy |
|---|---|---|---|
| list ports | port list |  n |
| n/a | port select | - |
| listen | listen | |
| device info | device info | |
| n/a | device reset | |
| n/a | device clock | |
| use port | config route | 
| mono enable | runtime enable | |
| mono disable | runtime disable | |
| mono state | runtime state | |
| file list | file list |
| file delete | file delete |
| file read | file read |
| file write | file write |
| file initial | file initial |
| n/a | firmware list |
| download os | firmware download |
| n/a | firmware default |
| n/a | firmware delete |
| flash esp | firmware write esp |
| flash os | firmware write os |
| mono flash | flash write runtime |
| mono update rt | flash write runtime |
| trace enable | trace enable |
| trace disable | trace disable |
| trace level | trace level |
| set developer | developer |
| uart trace | uart trace |
| n/a | app build |
| n/a | app trim |
| install dfu-util | dfu install |
| app deploy | app deply |
| n/a | app run |
| flash erase | flash erase |
| debug | TODO|
| device provision | TODO |
| package create | TODO |
| package list | TODO |
| package publish | TODO |
| package upload | TODO |
| collection list | TODO |
| cloud | TODO |




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
