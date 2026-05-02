using System.Diagnostics;
using System.IO;
using System.Windows;
using DataGateWin.Installer.Localization;
using MessageBox = System.Windows.MessageBox;

namespace DataGateWin.Installer;

internal static class UninstallRunner
{
    /// <summary>
    /// Removes shortcuts, registry entries, and the install directory.
    /// Install folder is taken from the uninstall registry key, then <paramref name="installDirectoryFallback"/>.
    /// </summary>
    public static async Task ExecuteAsync(bool quiet, string? installDirectoryFallback = null, Action<string>? log = null)
    {
        var installDir = InstallRegistry.TryGetInstallLocation()?.Trim();
        if (string.IsNullOrWhiteSpace(installDir))
            installDir = installDirectoryFallback?.Trim();

        if (string.IsNullOrWhiteSpace(installDir))
        {
            throw new InvalidOperationException(
                "Could not determine the installation folder. If the app was moved manually, uninstall from the installer UI or delete files yourself.");
        }

        void DefaultLog(string m) => Debug.WriteLine("[Uninstall] " + m);

        await ProcessStopCoordinator.EnsureAppProcessesStoppedAsync(
                interactivePrompts: !quiet,
                log: log ?? DefaultLog)
            .ConfigureAwait(false);

        log?.Invoke("Removing shortcuts...");
        ShortcutHelper.RemoveStartMenuShortcut();
        ShortcutHelper.RemoveDesktopShortcut();

        log?.Invoke("Removing registry entries...");
        InstallRegistry.UnregisterUninstallEntry();
        InstallRegistry.UnregisterAppPaths();

        if (Directory.Exists(installDir))
        {
            log?.Invoke("Deleting files...");
            Directory.Delete(installDir, recursive: true);
        }

        log?.Invoke("Done.");

        if (!quiet)
        {
            MessageBox.Show(
                InstallerLoc.T("Install_UninstallSuccessBody"),
                InstallerConstants.ProductName,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    public static void Execute(bool quiet, string? installDirectoryFallback = null, Action<string>? log = null)
    {
        ExecuteAsync(quiet, installDirectoryFallback, log).GetAwaiter().GetResult();
    }
}
