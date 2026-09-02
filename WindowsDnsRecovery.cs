using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DataGateWin.Installer;

internal interface IWindowsDnsRecoveryExecutor
{
    int RemoveStaleOpenVpnNrptRules(Action<string>? log);
    int RestoreOpenVpnSearchList(Action<string>? log);
    bool SignalDnsCacheReload(Action<string>? log);
    bool FlushDnsCache(Action<string>? log);
}

internal sealed class DefaultWindowsDnsRecoveryExecutor : IWindowsDnsRecoveryExecutor
{
    public static DefaultWindowsDnsRecoveryExecutor Instance { get; } = new();

    public int RemoveStaleOpenVpnNrptRules(Action<string>? log) =>
        WindowsDnsRecovery.RemoveStaleOpenVpnNrptRules(log);

    public int RestoreOpenVpnSearchList(Action<string>? log) =>
        WindowsDnsRecovery.RestoreOpenVpnSearchList(log);

    public bool SignalDnsCacheReload(Action<string>? log) =>
        WindowsDnsRecovery.SignalDnsCacheReload(log);

    public bool FlushDnsCache(Action<string>? log) =>
        WindowsDnsRecovery.FlushDnsCache(log);
}

/// <summary>
/// Removes stale OpenVPN NRPT / SearchList state left after crash/reboot/uninstall,
/// reloads Dnscache, then flushes DNS cache.
/// Mirrors engine <c>DnsStartupRecovery</c> and <c>TunWin::Setup::destroy()</c> DNS teardown.
/// </summary>
internal static class WindowsDnsRecovery
{
    internal const string OpenVpnNrptRulePrefix = "OpenVPNDNSRouting";

    internal const string DnsFlushCommand = "ipconfig /flushdns";

    /// <summary>Recovery step order shared with engine DnsStartupRecovery.cpp.</summary>
    internal static readonly string[] RecoveryStepOrder =
    [
        "remove_nrpt",
        "restore_search_list",
        "signal_dnscache",
        "flush_dns",
    ];

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

    /// <summary>
    /// Keys where openvpn3 may store SearchList / OriginalSearchList / InitialSearchList.
    /// Includes both "Windows NT" and openvpn3's "WindowsNT" spelling.
    /// </summary>
    internal static readonly string[] SearchListSubkeyPaths =
    [
        @"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient",
        @"SOFTWARE\Policies\Microsoft\WindowsNT\DNSClient",
        @"SYSTEM\CurrentControlSet\Services\TCPIP\Parameters",
    ];

    internal static bool IsOpenVpnNrptRuleName(string ruleName) =>
        ruleName.StartsWith(OpenVpnNrptRulePrefix, StringComparison.Ordinal);

    /// <summary>
    /// Pure SearchList restore decision (openvpn3 Dns::reset_search_domains).
    /// Returns null when neither backup value exists (leave SearchList untouched).
    /// </summary>
    internal static string? ResolveRestoredSearchList(string? originalSearchList, string? initialSearchList)
    {
        if (originalSearchList == null && initialSearchList == null)
            return null;

        return originalSearchList ?? string.Empty;
    }

    public static void RecoverStaleDnsState(
        Action<string>? log = null,
        IWindowsDnsRecoveryExecutor? executor = null)
    {
        var exec = executor ?? DefaultWindowsDnsRecoveryExecutor.Instance;
        exec.RemoveStaleOpenVpnNrptRules(log);
        exec.RestoreOpenVpnSearchList(log);
        exec.SignalDnsCacheReload(log);
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

    public static int RestoreOpenVpnSearchList(Action<string>? log = null)
    {
        var restored = 0;

        foreach (var view in NrptRegistryViews)
        {
            foreach (var subkeyPath in SearchListSubkeyPaths)
                restored += RestoreSearchListUnderKey(RegistryHive.LocalMachine, view, subkeyPath, log);
        }

        if (restored > 0)
            log?.Invoke($"Restored SearchList under {restored} registry key(s).");
        else
            log?.Invoke("No OpenVPN SearchList backup values found.");

        return restored;
    }

    public static bool SignalDnsCacheReload(Action<string>? log = null)
    {
        try
        {
            if (!NativeMethods.NotifyDnsCacheParamChange())
            {
                log?.Invoke("Dnscache PARAMCHANGE failed or service unavailable.");
                return false;
            }

            log?.Invoke("Signaled Dnscache PARAMCHANGE.");
            return true;
        }
        catch (Exception ex)
        {
            log?.Invoke($"Dnscache PARAMCHANGE failed: {ex.Message}");
            return false;
        }
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

    private static int RestoreSearchListUnderKey(
        RegistryHive hive,
        RegistryView view,
        string subkeyPath,
        Action<string>? log)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(subkeyPath, writable: true);
            if (key == null)
                return 0;

            var original = key.GetValue("OriginalSearchList") as string;
            var initial = key.GetValue("InitialSearchList") as string;
            var restored = ResolveRestoredSearchList(original, initial);
            if (restored == null)
                return 0;

            key.SetValue("SearchList", restored, RegistryValueKind.String);
            TryDeleteValue(key, "InitialSearchList");
            TryDeleteValue(key, "OriginalSearchList");

            log?.Invoke($"Restored SearchList under {subkeyPath} ({view}).");
            return 1;
        }
        catch (Exception ex)
        {
            log?.Invoke($"SearchList restore skipped for '{subkeyPath}' ({view}): {ex.Message}");
            return 0;
        }
    }

    private static void TryDeleteValue(RegistryKey key, string name)
    {
        try
        {
            key.DeleteValue(name, throwOnMissingValue: false);
        }
        catch
        {
            // best effort
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

    private static class NativeMethods
    {
        private const uint ServiceControlParamChange = 6;
        private const uint ServicePauseContinue = 0x0040;
        private const uint ScManagerConnect = 0x0001;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool ControlService(IntPtr hService, uint dwControl, ref ServiceStatus lpServiceStatus);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CloseServiceHandle(IntPtr hSCObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct ServiceStatus
        {
            public int dwServiceType;
            public int dwCurrentState;
            public int dwControlsAccepted;
            public int dwWin32ExitCode;
            public int dwServiceSpecificExitCode;
            public int dwCheckPoint;
            public int dwWaitHint;
        }

        public static bool NotifyDnsCacheParamChange()
        {
            var scm = OpenSCManager(null, null, ScManagerConnect);
            if (scm == IntPtr.Zero)
                return false;

            try
            {
                var svc = OpenService(scm, "Dnscache", ServicePauseContinue);
                if (svc == IntPtr.Zero)
                    return false;

                try
                {
                    var status = new ServiceStatus();
                    return ControlService(svc, ServiceControlParamChange, ref status);
                }
                finally
                {
                    CloseServiceHandle(svc);
                }
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        }
    }
}
