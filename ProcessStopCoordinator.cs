using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace DataGateWin.Installer;

internal static class ProcessStopCoordinator
{
    private const uint SnapshotProcess = 0x00000002;

    public static async Task EnsureAppProcessesStoppedAsync(bool interactivePrompts, Action<string>? log)
    {
        void L(string m)
        {
            log?.Invoke(m);
        }

        await Task.Delay(500).ConfigureAwait(false);
        var parentPid = GetParentProcessId();
        var processes = new[]
        {
            ("DataGateWin", "DataGateWin.exe"),
            ("engine", "engine.exe"),
        };

        var running = new List<Process>();
        foreach (var (processName, _) in processes)
        {
            try
            {
                running.AddRange(Process.GetProcessesByName(processName));
            }
            catch (Exception ex)
            {
                L($"WARN: failed to enumerate process {processName}: {ex.Message}");
            }
        }

        if (running.Count == 0)
            return;

        await Task.Delay(500).ConfigureAwait(false);
        var parentProcess = parentPid > 0
            ? running.FirstOrDefault(p => p.Id == parentPid)
            : null;

        if (interactivePrompts)
        {
            var names = string.Join(", ", running
                .Select(p => $"{p.ProcessName}.exe")
                .Distinct(StringComparer.OrdinalIgnoreCase));
            var result = MessageBox.Show(
                $"Detected running processes: {names}.{Environment.NewLine}Do you want to close them now?",
                InstallerConstants.ProductName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                throw new InvalidOperationException("Installation cancelled because the app is still running.");
        }

        foreach (var proc in running.DistinctBy(p => p.Id))
        {
            try
            {
                if (proc.HasExited)
                    continue;
                if (parentProcess != null && proc.Id == parentProcess.Id)
                {
                    L($"WARN: {proc.ProcessName} (PID {proc.Id}) started the installer; close it manually.");
                    continue;
                }

                L($"Stopping {proc.ProcessName} (PID {proc.Id})...");
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                L($"WARN: failed to stop {proc.ProcessName} (PID {proc.Id}): {ex.Message}");
            }
        }

        await Task.Delay(500).ConfigureAwait(false);
        var stillRunning = new List<Process>();
        foreach (var (processName, _) in processes)
        {
            try
            {
                stillRunning.AddRange(Process.GetProcessesByName(processName));
            }
            catch (Exception ex)
            {
                L($"WARN: failed to enumerate process {processName}: {ex.Message}");
            }
        }

        if (stillRunning.Count == 0)
            return;

        if (parentProcess != null && stillRunning.Any(p => p.Id == parentProcess.Id))
        {
            await Task.Delay(1500).ConfigureAwait(false);
            var refreshed = new List<Process>();
            foreach (var (processName, _) in processes)
            {
                try
                {
                    refreshed.AddRange(Process.GetProcessesByName(processName));
                }
                catch (Exception ex)
                {
                    L($"WARN: failed to enumerate process {processName}: {ex.Message}");
                }
            }

            stillRunning = refreshed;
            var parentStillRunning = stillRunning.Any(p => p.Id == parentProcess.Id);
            if (parentStillRunning)
            {
                throw new InvalidOperationException(
                    $"Installer was started by {parentProcess.ProcessName}. Please close it manually and retry.");
            }

            if (stillRunning.Count == 0)
                return;
        }

        if (stillRunning.Count > 0)
        {
            var stillNames = string.Join(", ", stillRunning
                .Select(p => $"{p.ProcessName}.exe")
                .Distinct(StringComparer.OrdinalIgnoreCase));

            if (!interactivePrompts)
            {
                foreach (var proc in stillRunning.DistinctBy(p => p.Id))
                {
                    try
                    {
                        if (!proc.HasExited)
                        {
                            proc.Kill(entireProcessTree: true);
                            proc.WaitForExit(3000);
                        }
                    }
                    catch (Exception ex)
                    {
                        L($"WARN: failed to stop {proc.ProcessName}: {ex.Message}");
                    }
                }

                await Task.Delay(300).ConfigureAwait(false);
                stillRunning.Clear();
                foreach (var (processName, _) in processes)
                {
                    try
                    {
                        stillRunning.AddRange(Process.GetProcessesByName(processName));
                    }
                    catch
                    {
                        // ignored
                    }
                }

                if (stillRunning.Count > 0)
                {
                    stillNames = string.Join(", ", stillRunning
                        .Select(p => $"{p.ProcessName}.exe")
                        .Distinct(StringComparer.OrdinalIgnoreCase));
                    throw new InvalidOperationException($"Processes are still running: {stillNames}");
                }

                return;
            }

            throw new InvalidOperationException($"Processes are still running: {stillNames}");
        }
    }

    private static int GetParentProcessId()
    {
        var currentId = Environment.ProcessId;
        var snapshot = CreateToolhelp32Snapshot(SnapshotProcess, 0);
        if (snapshot == IntPtr.Zero || snapshot == (IntPtr)(-1))
            return 0;

        try
        {
            var entry = new ProcessEntry32 { Size = (uint)Marshal.SizeOf<ProcessEntry32>() };
            if (!Process32First(snapshot, ref entry))
                return 0;

            do
            {
                if (entry.ProcessId == currentId)
                    return (int)entry.ParentProcessId;
            }
            while (Process32Next(snapshot, ref entry));

            return 0;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public IntPtr DefaultHeapId;
        public uint ModuleId;
        public uint ThreadCount;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
