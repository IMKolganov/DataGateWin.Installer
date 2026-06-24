namespace DataGateWin.Installer;

internal interface IInstallerUninstallSystem
{
    string? TryGetInstallLocation();
    Task StopAppProcessesAsync(bool quiet, Action<string> log, CancellationToken cancellationToken);
    void RecoverStaleDnsSettings(Action<string> log);
    void DeleteShortcuts();
    void RemoveRegistryEntries();
    void DeleteInstallDirectory(string installDirectory);
}

internal static class InstallerUninstaller
{
    public static async Task ExecuteAsync(
        IInstallerUninstallSystem system,
        bool quiet,
        string? fallbackInstallDirectory,
        Action<string> log,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(log);

        var installDirectory = ResolveInstallDirectory(
            system.TryGetInstallLocation(),
            fallbackInstallDirectory);

        log("Stopping DataGate processes...");
        await system.StopAppProcessesAsync(quiet, log, cancellationToken).ConfigureAwait(false);

        log("Recovering stale VPN DNS settings...");
        system.RecoverStaleDnsSettings(log);

        log("Removing shortcuts...");
        system.DeleteShortcuts();

        log("Removing registry entries...");
        system.RemoveRegistryEntries();

        if (string.IsNullOrWhiteSpace(installDirectory))
        {
            log("Install folder was not found; skipped folder deletion.");
            return;
        }

        log($"Deleting install folder: {installDirectory}");
        system.DeleteInstallDirectory(installDirectory);
        log("Uninstall completed.");
    }

    public static string? ResolveInstallDirectory(string? registryInstallDirectory, string? fallbackInstallDirectory)
    {
        if (!string.IsNullOrWhiteSpace(registryInstallDirectory))
            return registryInstallDirectory.Trim();

        return string.IsNullOrWhiteSpace(fallbackInstallDirectory)
            ? null
            : fallbackInstallDirectory.Trim();
    }
}
