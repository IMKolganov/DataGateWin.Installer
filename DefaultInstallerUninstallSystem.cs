using System.IO;

namespace DataGateWin.Installer;

internal sealed class DefaultInstallerUninstallSystem : IInstallerUninstallSystem
{
    public string? TryGetInstallLocation() =>
        InstallRegistry.TryGetInstallLocation();

    public Task StopAppProcessesAsync(bool quiet, Action<string> log, CancellationToken cancellationToken) =>
        ProcessStopCoordinator.EnsureAppProcessesStoppedAsync(interactivePrompts: !quiet, log);

    public void RecoverStaleDnsSettings(Action<string> log) =>
        WindowsDnsRecovery.RecoverStaleDnsState(log);

    public void DeleteShortcuts()
    {
        ShortcutHelper.RemoveStartMenuShortcut();
        ShortcutHelper.RemoveDesktopShortcut();
    }

    public void RemoveRegistryEntries()
    {
        InstallRegistry.UnregisterUninstallEntry();
        InstallRegistry.UnregisterAppPaths();
    }

    public void DeleteInstallDirectory(string installDirectory)
    {
        if (Directory.Exists(installDirectory))
            Directory.Delete(installDirectory, recursive: true);
    }
}
