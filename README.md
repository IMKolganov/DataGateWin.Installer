<p align="center">
  <img src="assets/logo.png" width="120" alt="DataGate" />
</p>

<h1 align="center">DataGate Installer</h1>
<p align="center"><strong>Windows 🪟 installer for DataGate VPN (OpenVPN over WSS)</strong></p>

<p align="center">
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078d6?logo=windows" alt="Windows 10/11" />
  <img src="https://img.shields.io/badge/.NET-WPF-512bd4?logo=dotnet" alt=".NET WPF" />
  <img src="https://img.shields.io/badge/DataGate-OpenVPN%20WSS-green" alt="DataGate OpenVPN WSS" />
</p>

---

## What is this?

**DataGate Installer** is a WPF installer for the Windows VPN app **DataGateWin** (DataGate OpenVPN 3). It downloads a release ZIP, extracts it to the selected folder, and registers the app in the system (Start Menu, Apps & Features, App Paths).

## Features

| Feature | Description |
|--------|-------------|
| **Wizard flow** | Policy, install path, shortcut options, and progress steps. |
| **Release download** | Downloads release ZIP by URL and extracts to disk. |
| **Shortcuts** | Optional Start Menu (under **Programs » DataGate**) and **shared (all users) Desktop** shortcuts (both on by default). |
| **Apps & Features** | Registers under `HKLM\...\Uninstall\DataGate` (older installs used `...\Uninstall\DataGateOpenVPN3`; both removed on uninstall) with `UninstallString` pointing at this installer plus `--uninstall`. |
| **App Paths** | Run `DataGateWin.exe` via **Win+R**. |
| **Update mode** | Update from the installer folder (`--update`; registry and shortcuts unchanged). |

## Requirements

- **Windows 10/11**
- **.NET SDK 10.0** (WPF, Windows)
- **Administrator rights** for HKLM writes (install/uninstall)

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

By default, the installer can query GitHub for the latest release and download the asset that matches `DataGateWin.v*.zip`. The ZIP must contain `DataGateWin.exe`. Optionally, it can include `favicon.ico` (used as the shortcut icon).

## Update mode

To update the app from the installer directory, run:

```bash
DataGateWin.Installer.exe --update
```

The installer looks for `DataGateWin.exe` next to itself and updates files in that folder.

## Uninstall

- **Settings » Apps**: runs `DataGateWin.Installer.exe --uninstall` (and `--quiet` for quiet uninstall). The installer removes both shortcuts, uninstall/App Paths registry keys, and deletes the install folder (`InstallLocation`).
- **Manual**: `DataGateWin.Installer.exe --uninstall` from an elevated prompt (same as Apps & Features).

Uninstall resolves the install folder from the registry first; a UI fallback path is only used if an uninstall control passes it (registry-first).

## Project layout

| Path | Description |
|------|-------------|
| **Services/** | Install / uninstall logic. |
| **Images/** | Installer UI images and icons. |
| **assets/** | Logo and images for the repo (e.g. README). |

## License

See `LICENSE.md`.
