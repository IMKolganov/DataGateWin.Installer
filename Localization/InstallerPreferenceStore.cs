using Microsoft.Win32;

namespace DataGateWin.Installer.Localization;

internal static class InstallerPreferenceStore
{
    const string KeyPath = @"Software\DataGate\Installer";

    public static string? ReadUiLanguagePreference()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue("UiLanguage") as string;
        }
        catch
        {
            return null;
        }
    }

    public static void WriteUiLanguagePreference(string preference)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
            key?.SetValue("UiLanguage", preference, RegistryValueKind.String);
        }
        catch
        {
            // ignored
        }
    }
}
