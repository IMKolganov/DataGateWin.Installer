using System.Diagnostics;
using System.IO;

namespace DataGateWin.Installer;

/// <summary>
/// Headless install used for local QA: same deploy/registry/shortcuts as the wizard,
/// but skips the download step when a <c>DataGateWinBuild*</c> folder is present.
/// </summary>
internal static class QuietLocalInstallRunner
{
    public static async Task ExecuteAsync(string packageDirectory, string installDirectory, Action<string>? log = null)
    {
        void L(string m)
        {
            log?.Invoke(m);
            Debug.WriteLine("[QuietLocalInstall] " + m);
        }

        if (string.IsNullOrWhiteSpace(packageDirectory) || !Directory.Exists(packageDirectory))
            throw new DirectoryNotFoundException("Local build package folder is missing.");

        var exeInPackage = Path.Combine(packageDirectory, InstallerConstants.ExeName);
        if (!File.Exists(exeInPackage))
            throw new FileNotFoundException("Local build package has no main exe.", exeInPackage);

        InstallerOperations.ValidatePackage(packageDirectory);

        if (string.IsNullOrWhiteSpace(installDirectory))
            throw new InvalidOperationException("Install folder is empty.");

        L($"Using local build folder (skip download): {packageDirectory}");
        L($"Installing to: {installDirectory}");

        await ProcessStopCoordinator.EnsureAppProcessesStoppedAsync(interactivePrompts: false, log: L, installDirectory: installDirectory);
        Directory.CreateDirectory(installDirectory);

        var installerExe = Process.GetCurrentProcess().MainModule?.FileName;
        var installerExePath = installerExe != null ? Path.GetFullPath(installerExe) : string.Empty;

        InstallerOperations.CopyDirectoryWithProgress(
            packageDirectory,
            installDirectory,
            dest => !string.IsNullOrWhiteSpace(installerExePath) &&
                    string.Equals(Path.GetFullPath(dest), installerExePath, StringComparison.OrdinalIgnoreCase),
            new Progress<double>(_ => { }),
            CancellationToken.None);

        var exePath = Path.Combine(installDirectory, InstallerConstants.ExeName);
        if (!File.Exists(exePath))
            throw new FileNotFoundException("Main executable was not found after copy.", exePath);

        L("Creating shortcuts...");
        ShortcutHelper.RefreshShortcuts(installDirectory, exePath, startMenu: true, desktop: true);

        L("Registering Apps & Features entry...");
        InstallRegistry.RegisterUninstallEntry(installDirectory);

        L("Registering App Paths...");
        InstallRegistry.RegisterAppPaths(exePath, installDirectory);

        L("Done.");
    }
}
