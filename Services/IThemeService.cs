using System.Windows;

namespace DataGateWin.Installer.Services;

public enum AppTheme
{
    Light,
    Dark
}

public interface IThemeService
{
    AppTheme GetSystemTheme();
    void ApplyTheme(ResourceDictionary resources, AppTheme mode);
}
