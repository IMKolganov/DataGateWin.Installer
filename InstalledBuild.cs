using System.Diagnostics;
using System.IO;
using System.Reflection;
using DataGateWin.Services.Update;

namespace DataGateWin.Installer;

internal static class InstalledBuild
{
    /// <summary>
    /// True if the on-disk main executable was built with the same version as this installer assembly.
    /// </summary>
    public static bool IsSameAsInstaller(string installedExePath)
        => IsSameAsInstaller(installedExePath, Assembly.GetExecutingAssembly().GetName().Version);

    /// <summary>Test seam: compare on-disk FileVersion to an explicit installer version.</summary>
    public static bool IsSameAsInstaller(string installedExePath, Version? installerAssemblyVersion)
    {
        return AppUpdatePolicy.IsSameBuild(TryReadVersion(installedExePath), installerAssemblyVersion);
    }

    public static Version? TryReadVersion(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return AppUpdatePolicy.TryParseVersion(info.FileVersion)
                ?? AppUpdatePolicy.TryParseVersion(info.ProductVersion);
        }
        catch { return null; }
    }

    public static void RequireNewerUpdate(string packageDirectory, string installDirectory)
    {
        var packageVersion = TryReadVersion(Path.Combine(packageDirectory, InstallerConstants.ExeName));
        var installedVersion = TryReadVersion(Path.Combine(installDirectory, InstallerConstants.ExeName));
        if (!AppUpdatePolicy.ShouldOfferUpgrade(packageVersion, installedVersion))
            throw new InvalidOperationException($"Update package ({packageVersion}) must be newer than installed build ({installedVersion}).");
    }
}
