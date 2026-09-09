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
        if (string.IsNullOrWhiteSpace(installedExePath) || !File.Exists(installedExePath))
            return false;

        Version? onDisk;
        try
        {
            var fvi = FileVersionInfo.GetVersionInfo(installedExePath);
            onDisk = AppUpdatePolicy.TryParseVersion(fvi.FileVersion)
                     ?? AppUpdatePolicy.TryParseVersion(fvi.ProductVersion);
        }
        catch
        {
            return false;
        }

        return AppUpdatePolicy.IsSameBuild(onDisk, installerAssemblyVersion);
    }
}
