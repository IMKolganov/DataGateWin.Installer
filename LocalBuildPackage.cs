using System.IO;

namespace DataGateWin.Installer;

/// <summary>
/// Offline install from a sibling folder named <c>DataGateWinBuild*</c> (explicit local publish layout).
/// When present next to the installer (or in its parent), GitHub download is skipped.
/// </summary>
internal static class LocalBuildPackage
{
    public const string DirectoryNamePrefix = "DataGateWinBuild";

    /// <summary>
    /// Finds the newest valid <c>DataGateWinBuild*</c> folder beside <paramref name="installerDirectory"/>
    /// or in its parent. Valid = contains <see cref="InstallerConstants.ExeName"/>.
    /// </summary>
    public static string? TryFind(string? installerDirectory)
    {
        if (string.IsNullOrWhiteSpace(installerDirectory))
            return null;

        string? best = null;
        var bestWriteUtc = DateTime.MinValue;

        foreach (var root in EnumerateSearchRoots(installerDirectory))
        {
            foreach (var candidate in EnumerateCandidates(root))
            {
                var writeUtc = Directory.GetLastWriteTimeUtc(candidate);
                if (best is null || writeUtc > bestWriteUtc)
                {
                    best = candidate;
                    bestWriteUtc = writeUtc;
                }
            }
        }

        return best;
    }

    public static bool IsLocalBuildDirectoryName(string? name)
        => !string.IsNullOrWhiteSpace(name) &&
           name.StartsWith(DirectoryNamePrefix, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumerateSearchRoots(string installerDirectory)
    {
        var dir = Path.GetFullPath(installerDirectory);
        yield return dir;

        var parent = Directory.GetParent(dir)?.FullName;
        if (!string.IsNullOrWhiteSpace(parent) &&
            !string.Equals(parent, dir, StringComparison.OrdinalIgnoreCase))
        {
            yield return parent;
        }
    }

    private static IEnumerable<string> EnumerateCandidates(string root)
    {
        if (!Directory.Exists(root))
            yield break;

        string[] children;
        try
        {
            children = Directory.GetDirectories(root);
        }
        catch
        {
            yield break;
        }

        foreach (var child in children)
        {
            var name = Path.GetFileName(child);
            if (!IsLocalBuildDirectoryName(name))
                continue;

            var exe = Path.Combine(child, InstallerConstants.ExeName);
            if (File.Exists(exe))
                yield return Path.GetFullPath(child);
        }
    }
}
