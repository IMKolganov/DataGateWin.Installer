# DataGateWin.Installer

WPF installer for the Windows app DataGateWin.
Downloads a release ZIP, extracts it to the selected folder, and registers the app in the system.

## Features

- wizard flow with policy, install path, shortcut options, and progress steps
- downloads release ZIP by URL and extracts it to disk
- optional shortcuts (both **on** by default): Start Menu folder under **Programs » DataGate**, and a shortcut on the **shared (all users) Desktop**
- registers an Apps & Features entry under `HKLM\...\Uninstall\DataGate` (older installs used `...\Uninstall\DataGateOpenVPN3`; both are removed on uninstall) with `UninstallString` pointing at this installer plus `--uninstall`
- registers App Paths so `DataGateWin.exe` resolves from `Win+R`
- update mode from the installer folder (`--update`; registry and shortcuts are left unchanged)

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
3. Choose the install path and whether to create Start Menu / Desktop shortcuts (defaults: both enabled). After accepting the policy, if this installer build matches the installed app version, you can launch the app, continue to reinstall files, or exit.
4. Wait for the installation to finish.

By default, the installer queries GitHub for the latest release and downloads
the asset that matches `DataGateWin.v*.zip`.

The ZIP must contain `DataGateWin.exe`. Optionally, it can include `favicon.ico`
(used as the shortcut icon).

## Update Mode

To update the app from the installer directory, run:

```bash
DataGateWin.Installer.exe --update
```

The installer checks for `DataGateWin.exe` next to itself and updates files in that folder.

## Uninstall

- **Settings » Apps**: runs `DataGateWin.Installer.exe --uninstall` (and `--quiet` for quiet uninstall). The installer removes both shortcuts, uninstall/App Paths registry keys, and deletes the install folder (`InstallLocation`).
- **Manual**: `DataGateWin.Installer.exe --uninstall` from an elevated prompt (same as Apps & Features).

Uninstall resolves the install folder from the registry first; a UI fallback path is only used if an uninstall control passes it (registry-first).

## License

See `LICENSE.md`.
