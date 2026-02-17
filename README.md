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
| **Wizard flow** | Policy, install path, and progress steps. |
| **Release download** | Downloads release ZIP by URL and extracts to disk. |
| **Start Menu shortcut** | Creates a shortcut for the app. |
| **Apps & Features** | Registers an uninstall entry. |
| **App Paths** | Run `DataGateWin.exe` via Win+R. |
| **Update mode** | Update the app from the installer folder (`--update`). |

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
3. Choose the install path.
4. Wait for the installation to finish.

By default, the installer can query GitHub for the latest release and download the asset that matches `DataGateWin.v*.zip`. The ZIP must contain `DataGateWin.exe`. Optionally, it can include `favicon.ico` (used as the shortcut icon).

## Update mode

To update the app from the installer directory, run:

```bash
DataGateWin.Installer.exe --update
```

The installer looks for `DataGateWin.exe` next to itself and updates files in that folder.

## Uninstall

Use the standard uninstall flow via **Windows Apps & Features**, or the uninstall option inside the app (if available).

## Project layout

| Path | Description |
|------|-------------|
| **Services/** | Install / uninstall logic. |
| **Images/** | Installer UI images and icons. |
| **assets/** | Logo and images for the repo (e.g. README). |

## License

See `LICENSE.md`.
