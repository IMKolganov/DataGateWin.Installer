using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace DataGateWin.Installer;

internal enum StartupDnsRecoveryResult { NoChanges, Recovered, ActiveSession, Failed }

/// <summary>Offline recovery, independent of installed engine files and login.</summary>
internal static class StartupDnsRecovery
{
    internal static StartupDnsRecoveryResult Run(
        Action<string> log,
        Func<bool>? isSessionActive = null,
        Func<bool>? hasArtifacts = null,
        Action? recover = null)
    {
        try
        {
            if ((isSessionActive ?? IsSessionActive)())
            {
                log("DNS recovery skipped: a VPN engine may still be active.");
                return StartupDnsRecoveryResult.ActiveSession;
            }
            var probe = hasArtifacts ?? HasArtifacts;
            if (!probe())
            {
                log("No leftover OpenVPN DNS settings detected.");
                return StartupDnsRecoveryResult.NoChanges;
            }
            log("Leftover OpenVPN DNS settings detected; recovering before network requests.");
            // Recheck immediately before mutation, including live NRPT owners.
            if ((isSessionActive ?? IsSessionActive)())
                return StartupDnsRecoveryResult.ActiveSession;
            (recover ?? (() => WindowsDnsRecovery.RecoverStaleDnsState(log)))();
            if (probe())
            {
                log("DNS recovery incomplete: leftover settings remain.");
                return StartupDnsRecoveryResult.Failed;
            }
            log("Leftover OpenVPN DNS settings recovered.");
            return StartupDnsRecoveryResult.Recovered;
        }
        catch (Exception ex)
        {
            log($"DNS startup check failed: {ex.Message}");
            return StartupDnsRecoveryResult.Failed;
        }
    }

    private static bool IsSessionActive()
    {
        // Conservatively leave live engines alone, including other OpenVPN clients.
        foreach (var name in new[] { "engine", "openvpn", "openvpnserv", "ovpnagent" })
        {
            var processes = Process.GetProcessesByName(name);
            try { if (processes.Length != 0) return true; }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        if (Directory.EnumerateFiles(@"\\.\pipe\")
            .Select(Path.GetFileName)
            .Any(name => name != null && name.StartsWith("datagate.engine.", StringComparison.Ordinal)
                && name.EndsWith(".control", StringComparison.Ordinal)))
            return true;

        foreach (var view in WindowsDnsRecovery.NrptRegistryViews)
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            foreach (var path in WindowsDnsRecovery.NrptSubkeyPaths)
            {
                using var key = root.OpenSubKey(path);
                if (key == null) continue;
                foreach (var name in key.GetSubKeyNames().Where(WindowsDnsRecovery.IsOpenVpnNrptRuleName))
                {
                    if (!int.TryParse(name[(name.LastIndexOf('-') + 1)..], out var pid) || pid <= 0)
                        continue;
                    try
                    {
                        using var owner = Process.GetProcessById(pid);
                        if (!owner.HasExited) return true;
                    }
                    catch (ArgumentException) { /* owner no longer exists */ }
                    // Access errors propagate: uncertain ownership must prevent cleanup.
                }
            }
        }
        return false;
    }

    private static bool HasArtifacts()
    {
        foreach (var view in WindowsDnsRecovery.NrptRegistryViews)
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            foreach (var path in WindowsDnsRecovery.NrptSubkeyPaths)
            {
                using var key = root.OpenSubKey(path);
                if (key?.GetSubKeyNames().Any(WindowsDnsRecovery.IsOpenVpnNrptRuleName) == true)
                    return true;
            }
            foreach (var path in WindowsDnsRecovery.SearchListSubkeyPaths)
            {
                using var key = root.OpenSubKey(path);
                if (key?.GetValue("OriginalSearchList") is string || key?.GetValue("InitialSearchList") is string)
                    return true;
            }
        }
        return false;
    }
}
