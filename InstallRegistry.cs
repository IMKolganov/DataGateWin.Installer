using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;

namespace DataGateWin.Installer;

internal static class InstallRegistry
{
    /// <summary>Reads InstallLocation from the current uninstall key, then the legacy OpenVPN-era key.</summary>
    public static string? TryGetInstallLocation()
    {
        foreach (var relativePath in UninstallKeyRelativePaths)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(relativePath);
                var loc = key?.GetValue("InstallLocation") as string;
                if (!string.IsNullOrWhiteSpace(loc))
                    return loc.Trim();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        return null;
    }

    static string[] UninstallKeyRelativePaths =>
    [
        InstallerConstants.UninstallRegKeyPath,
        InstallerConstants.LegacyUninstallRegKeyPath,
    ];

    public static void RegisterUninstallEntry(string installDir)
    {
        var installerExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(installerExe))
            throw new InvalidOperationException("Cannot resolve installer path.");

        using var key = Registry.LocalMachine.CreateSubKey(InstallerConstants.UninstallRegKeyPath, writable: true);
        if (key == null)
            throw new InvalidOperationException("Failed to open HKLM uninstall key. Run as administrator.");

        key.SetValue("DisplayName", InstallerConstants.ProductName, RegistryValueKind.String);
        key.SetValue("Publisher", InstallerConstants.Publisher, RegistryValueKind.String);
        key.SetValue("InstallLocation", installDir, RegistryValueKind.String);

        var version = GetInstallerVersion();
        if (!string.IsNullOrWhiteSpace(version))
            key.SetValue("DisplayVersion", version, RegistryValueKind.String);

        key.SetValue("UninstallString", $"\"{installerExe}\" --uninstall", RegistryValueKind.String);
        key.SetValue("QuietUninstallString", $"\"{installerExe}\" --uninstall --quiet", RegistryValueKind.String);

        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

        try
        {
            Registry.LocalMachine.DeleteSubKeyTree(InstallerConstants.LegacyUninstallRegKeyPath, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    public static void UnregisterUninstallEntry()
    {
        foreach (var relativePath in UninstallKeyRelativePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(relativePath, throwOnMissingSubKey: false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }

    public static void RegisterAppPaths(string exePath, string installDir)
    {
        using var key = Registry.LocalMachine.CreateSubKey(InstallerConstants.AppPathsRegKeyPath, writable: true);
        if (key == null)
            throw new InvalidOperationException("Failed to open HKLM App Paths key. Run as administrator.");

        key.SetValue(string.Empty, exePath, RegistryValueKind.String);
        key.SetValue("Path", installDir, RegistryValueKind.String);
    }

    public static void UnregisterAppPaths()
    {
        try
        {
            Registry.LocalMachine.DeleteSubKeyTree(InstallerConstants.AppPathsRegKeyPath, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    static string? GetInstallerVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var v = asm.GetName().Version;
        return v?.ToString();
    }
}
