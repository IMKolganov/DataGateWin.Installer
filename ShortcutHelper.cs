using System.IO;

namespace DataGateWin.Installer;

internal static class ShortcutHelper
{
    static string GetStartMenuFolder()
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
        return Path.Combine(programs, InstallerConstants.StartMenuRelativeFolder);
    }

    public static void CreateStartMenuShortcut(string installDir, string exePath)
    {
        var folder = GetStartMenuFolder();
        Directory.CreateDirectory(folder);

        var shortcutPath = Path.Combine(folder, $"{InstallerConstants.ProductName}.lnk");
        WriteShortcut(shortcutPath, installDir, exePath);
    }

    public static void RemoveStartMenuShortcut()
    {
        var folder = GetStartMenuFolder();
        var shortcutPath = Path.Combine(folder, $"{InstallerConstants.ProductName}.lnk");

        if (File.Exists(shortcutPath))
            File.Delete(shortcutPath);

        if (Directory.Exists(folder) &&
            Directory.GetFiles(folder).Length == 0 &&
            Directory.GetDirectories(folder).Length == 0)
        {
            Directory.Delete(folder);
        }
    }

    /// <summary>Per-machine shortcut on the shared desktop (visible to all users).</summary>
    public static void CreateDesktopShortcut(string installDir, string exePath)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        Directory.CreateDirectory(desktop);

        var shortcutPath = Path.Combine(desktop, $"{InstallerConstants.ProductName}.lnk");
        WriteShortcut(shortcutPath, installDir, exePath);
    }

    public static void RemoveDesktopShortcut()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        var shortcutPath = Path.Combine(desktop, $"{InstallerConstants.ProductName}.lnk");

        if (File.Exists(shortcutPath))
            File.Delete(shortcutPath);
    }

    public static void RemoveLegacyShortcuts()
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

        foreach (var name in InstallerConstants.LegacyShortcutNames)
        {
            TryDelete(Path.Combine(programs, name));
            TryDelete(Path.Combine(programs, InstallerConstants.StartMenuRelativeFolder, name));
            TryDelete(Path.Combine(desktop, name));

            foreach (var folder in InstallerConstants.LegacyStartMenuRelativeFolders)
                TryDelete(Path.Combine(programs, folder, name));
        }

        foreach (var folder in InstallerConstants.LegacyStartMenuRelativeFolders)
            TryDeleteEmptyDirectory(Path.Combine(programs, folder));
    }

    public static void RefreshShortcuts(string installDir, string exePath, bool startMenu, bool desktop)
    {
        RemoveLegacyShortcuts();

        if (startMenu)
            CreateStartMenuShortcut(installDir, exePath);

        if (desktop)
            CreateDesktopShortcut(installDir, exePath);
    }

    public static bool LegacyOrCurrentDesktopShortcutExists()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        if (File.Exists(Path.Combine(desktop, $"{InstallerConstants.ProductName}.lnk")))
            return true;

        return InstallerConstants.LegacyShortcutNames
            .Any(name => File.Exists(Path.Combine(desktop, name)));
    }

    static void TryDelete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    static void TryDeleteEmptyDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        if (Directory.GetFiles(path).Length == 0 && Directory.GetDirectories(path).Length == 0)
            Directory.Delete(path);
    }

    static void WriteShortcut(string shortcutPath, string installDir, string exePath)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = exePath;
        shortcut.WorkingDirectory = installDir;
        shortcut.WindowStyle = 1;
        shortcut.Description = InstallerConstants.ProductName;

        var iconPath = Path.Combine(installDir, "Images", "favicon.ico");
        if (File.Exists(iconPath))
            shortcut.IconLocation = iconPath;
        else if (File.Exists(exePath))
            shortcut.IconLocation = exePath;

        shortcut.Save();
    }
}
