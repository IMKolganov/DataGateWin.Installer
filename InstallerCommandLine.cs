namespace DataGateWin.Installer;

internal readonly record struct InstallerCommandLineOptions(bool IsUpdateMode, bool IsUninstall, bool Quiet);

internal static class InstallerCommandLine
{
    public static InstallerCommandLineOptions Parse(IEnumerable<string> args)
    {
        var isUpdateMode = false;
        var isUninstall = false;
        var quiet = false;

        foreach (var arg in args)
        {
            if (string.Equals(arg, "update", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--update", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "/update", StringComparison.OrdinalIgnoreCase))
            {
                isUpdateMode = true;
            }

            if (string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase))
                isUninstall = true;

            if (string.Equals(arg, "--quiet", StringComparison.OrdinalIgnoreCase))
                quiet = true;
        }

        return new InstallerCommandLineOptions(isUpdateMode, isUninstall, quiet);
    }
}
