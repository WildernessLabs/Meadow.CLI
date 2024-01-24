<img alt="Meadow CLI project banner stating Meadow's Command-Line-Interface to interact with the board and perform functions via a terminal/command-line window." src="Design/banner.jpg" style="margin-bottom:10px" />

## Build Status
[![Build](https://github.com/WildernessLabs/Meadow.CLI/actions/workflows/dotnet.yml/badge.svg)](https://github.com/WildernessLabs/Meadow.CLI/actions)

## Getting Started

To install the latest Meadow.CLI release, run the .NET tool install command to get the latest package from NuGet.

```console
dotnet tool install WildernessLabs.Meadow.CLI --global
```

For the latest getting started instructions with Meadow and Meadow.CLI, check out the [Meadow guides](https://developer.wildernesslabs.co/Meadow/Getting_Started/Deploying_Meadow/) in the Wilderness Labs documentations. Additionally, there are instructions there for updating an existing Meadow.CLI install.

If you want to develop or build a Meadow.CLI directly, or install a pre-release version, follow the [instructions to install a pre-release Meadow.CLI](https://github.com/WildernessLabs/Meadow.CLI/blob/develop/README.md#install-a-downloaded-pre-release-version).

Once installed, run the Meadow.CLI from a command line with `meadow`.

## Options

To see the options, run the application with the --help arg.

## Useful commands

### Download Meadow OS

```
meadow firmware download
```

### Update the Meadow OS

```
meadow firmware write
```

### Listen for Meadow Console.WriteLine
```
meadow listen
```

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

### Meadow.CLI download location

If you need to find or clear out any of the OS download files retrieved by Meadow.CLI, they are located in a WildernessLabs folder in the user directory.

macOS: `~/.local/share/WildernessLabs/Firmware/`
Windows: `%LOCALAPPDATA%\WildernessLabs\Firmware`
