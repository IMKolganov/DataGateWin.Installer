using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace DataGateWin.Installer;

internal static class InstalledBuild
{
    /// <summary>
    /// True if the on-disk main executable was built with the same version as this installer assembly.
    /// </summary>
    public static bool IsSameAsInstaller(string installedExePath)
    {
        if (string.IsNullOrWhiteSpace(installedExePath) || !File.Exists(installedExePath))
            return false;

        var fvi = FileVersionInfo.GetVersionInfo(installedExePath);
        var onDisk = TryParseVersion(fvi.FileVersion) ?? TryParseVersion(fvi.ProductVersion);
        var asm = Assembly.GetExecutingAssembly().GetName().Version;
        if (onDisk is null || asm is null)
            return false;

        return onDisk.Major == asm.Major
            && onDisk.Minor == asm.Minor
            && onDisk.Build == asm.Build
            && onDisk.Revision == asm.Revision;
    }

    static Version? TryParseVersion(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;
        try
        {
            return new Version(s.Trim());
        }
        catch
        {
            return Version.TryParse(s.Trim(), out var v) ? v : null;
        }
    }
}
