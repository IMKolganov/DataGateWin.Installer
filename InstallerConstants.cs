namespace DataGateWin.Installer;

internal static class InstallerConstants
{
    /// <summary>Shown in Start Menu, Apps &amp; Features, and shortcuts (protocol-neutral).</summary>
    public const string ProductName = "DataGate";
    public const string Publisher = "DataGate";
    public const string ExeName = "DataGateWin.exe";
    public const string BundledInstallerRelativePath = @"Installer\DataGateWin.Installer.exe";

    public const string UninstallRegKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\DataGate";

    /// <summary>Older installers registered here; still honored for detection and removal.</summary>
    public const string LegacyUninstallRegKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\DataGateOpenVPN3";
    public const string AppPathsRegKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\DataGateWin.exe";

    /// <summary>Subfolder under the common Start Menu Programs folder.</summary>
    public const string StartMenuRelativeFolder = "DataGate";
}
