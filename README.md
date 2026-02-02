# DataGateWin.Installer

WPF installer for the Windows app DataGateWin (DataGate OpenVPN 3).
Downloads a release ZIP, extracts it to the selected folder, and registers the app in the system.

## Features

- wizard flow with policy, install path, and progress steps
- downloads release ZIP by URL and extracts it to disk
- creates a Start Menu shortcut
- registers an Apps & Features entry
- registers App Paths to run `DataGateWin.exe` via `Win+R`
- update mode from the installer folder

## Requirements

- Windows 10/11
- .NET SDK 10.0 (WPF, Windows)
- administrator rights for HKLM writes (install/uninstall)

## Build

Open `DataGateWin.Installer.sln` in Visual Studio and build the project.

Or via CLI:

```bash
dotnet build .\DataGateWin.Installer.csproj -c Release
```

## Usage

1. Run the installer.
2. Accept the policy.
3. Choose the install path.
4. Wait for the installation to finish.

By default, the release ZIP is downloaded from GitHub Releases:
`https://github.com/IMKolganov/DataGateWin/releases/latest/download/DataGate.v1.0.0.zip`

The ZIP must contain `DataGateWin.exe`. Optionally, it can include `favicon.ico`
(used as the shortcut icon).

## Update Mode

To update the app from the installer directory, run:

```bash
DataGateWin.Installer.exe --update
```

The installer checks for `DataGateWin.exe` next to itself and updates files in that folder.

## Uninstall

Use the standard uninstall flow via Windows Apps & Features,
or the uninstall button inside the app (if available).

## License

See `LICENSE.md`.
