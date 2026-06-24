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
        void DefaultLog(string m) => Debug.WriteLine("[Uninstall] " + m);
        var logger = log ?? DefaultLog;

        await InstallerUninstaller.ExecuteAsync(
                new DefaultInstallerUninstallSystem(),
                quiet,
                installDirectoryFallback,
                logger)
            .ConfigureAwait(false);

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
