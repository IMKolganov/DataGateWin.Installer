using System.IO;

namespace DataGateWin.Installer;

internal static class InstallerOperations
{
    public static string ResolveUpdateInstallDir(
        string startDir,
        string exeName,
        Action<string>? log = null,
        int maxDepth = 6)
    {
        var current = startDir;
        var checkedDirs = new List<string>();

        for (var depth = 0; depth < maxDepth && !string.IsNullOrWhiteSpace(current); depth++)
        {
            checkedDirs.Add(current);
            var candidateExe = Path.Combine(current, exeName);
            if (File.Exists(candidateExe))
            {
                log?.Invoke($"Update mode: resolved install folder: {current}");
                return current;
            }

            var parent = Directory.GetParent(current)?.FullName;
            if (string.IsNullOrWhiteSpace(parent) ||
                string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent;
        }

        var attempts = string.Join(Environment.NewLine, checkedDirs.Select(d => $" - {d}"));
        throw new FileNotFoundException(
            $"DataGateWin.exe was not found near the installer. Checked:{Environment.NewLine}{attempts}",
            Path.Combine(startDir, exeName));
    }

    public static void CopyDirectoryWithProgress(
        string sourceDir,
        string destinationDir,
        Func<string, bool>? skipDestination,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDir);

        var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        var copyList = new List<(string Source, string Destination)>();

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destFile = Path.Combine(destinationDir, relative);

            if (skipDestination?.Invoke(destFile) == true)
                continue;

            copyList.Add((file, destFile));
        }

        if (copyList.Count == 0)
        {
            progress.Report(100);
            return;
        }

        for (var i = 0; i < copyList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (source, dest) = copyList[i];
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(source, dest, overwrite: true);
            progress.Report((i + 1) * 100.0 / copyList.Count);
        }
    }
}
