using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace DataGateWin.Installer;

internal interface IWindowsDnsRecoveryExecutor
{
    int RemoveStaleOpenVpnNrptRules(Action<string>? log);
    bool FlushDnsCache(Action<string>? log);
}

internal sealed class DefaultWindowsDnsRecoveryExecutor : IWindowsDnsRecoveryExecutor
{
    public static DefaultWindowsDnsRecoveryExecutor Instance { get; } = new();

    public int RemoveStaleOpenVpnNrptRules(Action<string>? log) =>
        WindowsDnsRecovery.RemoveStaleOpenVpnNrptRules(log);

    public bool FlushDnsCache(Action<string>? log) =>
        WindowsDnsRecovery.FlushDnsCache(log);
}

/// <summary>
/// Removes stale OpenVPN NRPT rules left after crash/reboot/uninstall, then flushes DNS cache.
/// Mirrors engine startup recovery and <c>TunWin::Setup::destroy()</c> DNS teardown in openvpn3.
/// </summary>
internal static class WindowsDnsRecovery
{
    internal const string OpenVpnNrptRulePrefix = "OpenVPNDNSRouting";

    internal const string DnsFlushCommand = "ipconfig /flushdns";

    internal static readonly RegistryView[] NrptRegistryViews =
    [
        RegistryView.Registry64,
        RegistryView.Registry32,
    ];

    internal static readonly string[] NrptSubkeyPaths =
    [
        @"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient\DnsPolicyConfig",
        @"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\DnsPolicyConfig",
    ];

    internal static bool IsOpenVpnNrptRuleName(string ruleName) =>
        ruleName.StartsWith(OpenVpnNrptRulePrefix, StringComparison.Ordinal);

    public static void RecoverStaleDnsState(
        Action<string>? log = null,
        IWindowsDnsRecoveryExecutor? executor = null)
    {
        var exec = executor ?? DefaultWindowsDnsRecoveryExecutor.Instance;
        exec.RemoveStaleOpenVpnNrptRules(log);
        exec.FlushDnsCache(log);
    }

    public static int RemoveStaleOpenVpnNrptRules(Action<string>? log = null)
    {
        var removed = 0;

        foreach (var view in NrptRegistryViews)
        {
            foreach (var subkeyPath in NrptSubkeyPaths)
            {
                removed += RemoveMatchingSubkeys(
                    RegistryHive.LocalMachine,
                    view,
                    subkeyPath,
                    IsOpenVpnNrptRuleName,
                    log);
            }
        }

        if (removed > 0)
            log?.Invoke($"Removed {removed} stale OpenVPN NRPT rule(s).");
        else
            log?.Invoke("No stale OpenVPN NRPT rules found.");

        return removed;
    }

    public static bool FlushDnsCache(Action<string>? log = null)
    {
        try
        {
            var ipconfigPath = Path.Combine(Environment.SystemDirectory, "ipconfig.exe");
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = ipconfigPath,
                Arguments = "/flushdns",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (process == null)
            {
                log?.Invoke("DNS cache flush failed: process not started.");
                return false;
            }

            if (!process.WaitForExit(5000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                log?.Invoke("DNS cache flush failed: timed out.");
                return false;
            }

            if (process.ExitCode == 0)
            {
                log?.Invoke("DNS cache flushed.");
                return true;
            }

            log?.Invoke($"DNS cache flush failed: exit code {process.ExitCode}.");
            return false;
        }
        catch (Exception ex)
        {
            log?.Invoke($"DNS cache flush failed: {ex.Message}");
            return false;
        }
    }

    private static int RemoveMatchingSubkeys(
        RegistryHive hive,
        RegistryView view,
        string subkeyPath,
        Func<string, bool> shouldRemove,
        Action<string>? log)
    {
        var removed = 0;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var nrptKey = baseKey.OpenSubKey(subkeyPath, writable: true);
            if (nrptKey == null)
                return 0;

            foreach (var ruleName in nrptKey.GetSubKeyNames())
            {
                if (!shouldRemove(ruleName))
                    continue;

                try
                {
                    nrptKey.DeleteSubKeyTree(ruleName, throwOnMissingSubKey: false);
                    removed++;
                    log?.Invoke($"Removed NRPT rule: {ruleName} ({subkeyPath})");
                }
                catch (Exception ex)
                {
                    log?.Invoke($"Failed to remove NRPT rule '{ruleName}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            log?.Invoke($"NRPT cleanup skipped for '{subkeyPath}' ({view}): {ex.Message}");
        }

        return removed;
    }
}
