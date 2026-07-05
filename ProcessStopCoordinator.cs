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
        void L(string m) => log?.Invoke(m);

        await Task.Delay(500).ConfigureAwait(false);
        var parentPid = GetParentProcessId();
        var processes = new[]
        {
            ("DataGateWin", "DataGateWin.exe"),
            ("engine", "engine.exe"),
        };

        using var runningScope = CollectProcesses(processes, L);
        var running = runningScope.Processes;
        if (running.Count == 0)
            return;

        await Task.Delay(500).ConfigureAwait(false);
        var parentProcess = parentPid > 0
            ? running.FirstOrDefault(p => p.Id == parentPid)
            : null;

        if (interactivePrompts)
        {
            var names = FormatProcessNames(running);
            var result = PromptCloseProcesses(names);

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
        using var stillRunningScope = CollectProcesses(processes, L);
        var stillRunning = stillRunningScope.Processes;
        if (stillRunning.Count == 0)
            return;

        if (parentProcess != null && stillRunning.Any(p => p.Id == parentProcess.Id))
        {
            await Task.Delay(1500).ConfigureAwait(false);
            using var refreshedScope = CollectProcesses(processes, L);
            stillRunning = refreshedScope.Processes;
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
            var stillNames = FormatProcessNames(stillRunning);

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
                using var finalScope = CollectProcesses(processes, null);
                stillRunning = finalScope.Processes;

                if (stillRunning.Count > 0)
                {
                    stillNames = FormatProcessNames(stillRunning);
                    throw new InvalidOperationException($"Processes are still running: {stillNames}");
                }

                return;
            }

            throw new InvalidOperationException($"Processes are still running: {stillNames}");
        }
    }

    private static string FormatProcessNames(IEnumerable<Process> processes)
        => string.Join(", ", processes
            .Select(p => $"{p.ProcessName}.exe")
            .Distinct(StringComparer.OrdinalIgnoreCase));

    private static ProcessScope CollectProcesses(
        (string ProcessName, string FileName)[] processes,
        Action<string>? log)
    {
        var running = new List<Process>();
        foreach (var (processName, _) in processes)
        {
            try
            {
                running.AddRange(Process.GetProcessesByName(processName));
            }
            catch (Exception ex)
            {
                log?.Invoke($"WARN: failed to enumerate process {processName}: {ex.Message}");
            }
        }

        return new ProcessScope(running);
    }

    private static MessageBoxResult PromptCloseProcesses(string names)
    {
        MessageBoxResult result = MessageBoxResult.No;
        InstallerUiThread.Run(() =>
        {
            result = MessageBox.Show(
                $"Detected running processes: {names}.{Environment.NewLine}Do you want to close them now?",
                InstallerConstants.ProductName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
        });
        return result;
    }

    private sealed class ProcessScope : IDisposable
    {
        public ProcessScope(List<Process> processes) => Processes = processes;

        public List<Process> Processes { get; }

        public void Dispose()
        {
            foreach (var process in Processes)
            {
                try
                {
                    process.Dispose();
                }
                catch
                {
                    // ignored
                }
            }
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
