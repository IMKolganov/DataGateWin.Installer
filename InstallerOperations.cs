using System.IO;

namespace DataGateWin.Installer;

internal static class InstallerOperations
{
    public static void ValidatePackage(string sourceDirectory)
    {
        var executable = Path.Combine(sourceDirectory, InstallerConstants.ExeName);
        if (!File.Exists(executable) || new FileInfo(executable).Length == 0)
            throw new InvalidDataException("Installation package is missing a nonempty main executable.");
    }

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
        cancellationToken.ThrowIfCancellationRequested();
        sourceDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceDir));
        destinationDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationDir));
        if (IsWithin(sourceDir, destinationDir) || IsWithin(destinationDir, sourceDir))
            throw new InvalidOperationException("Package and installation directories must not overlap.");

        var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        var copyList = new List<(string Source, string Destination)>();

        foreach (var file in files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            // Offline local packages live as DataGateWinBuild* beside the installer — never nest them.
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(LocalBuildPackage.IsLocalBuildDirectoryName))
                continue;

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

        var backupDirectory = Path.Combine(Path.GetTempPath(), "DataGateRollback", Guid.NewGuid().ToString("N"));
        var replaced = new List<(string Destination, string? Backup)>();
        var preserveBackup = false;
        Directory.CreateDirectory(backupDirectory);
        try
        {
            for (var i = 0; i < copyList.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (source, dest) = copyList[i];
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                var staged = dest + "." + Guid.NewGuid().ToString("N") + ".pending";
                try
                {
                    File.Copy(source, staged);
                    cancellationToken.ThrowIfCancellationRequested();
                    string? backup = null;
                    if (File.Exists(dest))
                    {
                        backup = Path.Combine(backupDirectory, i.ToString());
                        File.Copy(dest, backup);
                    }
                    File.Move(staged, dest, overwrite: true);
                    replaced.Add((dest, backup));
                }
                finally
                {
                    if (File.Exists(staged)) File.Delete(staged);
                }
                progress.Report((i + 1) * 100.0 / copyList.Count);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        catch (Exception failure)
        {
            var rollbackFailures = new List<Exception>();
            foreach (var (destination, backup) in replaced.AsEnumerable().Reverse())
            {
                try
                {
                    if (backup is null) File.Delete(destination);
                    else File.Copy(backup, destination, overwrite: true);
                }
                catch (Exception ex) { rollbackFailures.Add(ex); }
            }
            if (rollbackFailures.Count > 0)
            {
                preserveBackup = true;
                throw new AggregateException($"Deployment and rollback failed. Backups retained at {backupDirectory}.",
                    new[] { failure }.Concat(rollbackFailures));
            }
            throw;
        }
        finally
        {
            if (!preserveBackup)
            {
                try { Directory.Delete(backupDirectory, recursive: true); }
                catch (IOException) { /* A stale backup must not turn a completed deployment into a failure. */ }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static bool IsWithin(string path, string directory)
        => string.Equals(path, directory, StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith(Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar,
               StringComparison.OrdinalIgnoreCase);
}
