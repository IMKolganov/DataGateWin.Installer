using System.Globalization;
using System.Windows;

namespace DataGateWin.Installer.Localization;

public static class InstallerLoc
{
    public static string T(string key)
    {
        if (System.Windows.Application.Current?.TryFindResource(key) is string s && s.Length > 0)
            return s;
        return key;
    }

    public static string T(string key, params object?[] args)
    {
        var template = T(key);
        if (args is null || args.Length == 0)
            return template;
        try
        {
            return string.Format(CultureInfo.CurrentUICulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }
}
