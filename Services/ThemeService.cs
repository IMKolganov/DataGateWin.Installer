using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace DataGateWin.Installer.Services;

public sealed class ThemeService : IThemeService
{
    public AppTheme GetSystemTheme()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1);
            return value is int v && v == 0 ? AppTheme.Dark : AppTheme.Light;
        }
        catch
        {
            return AppTheme.Light;
        }
    }

    public void ApplyTheme(ResourceDictionary resources, AppTheme mode)
    {
        if (mode == AppTheme.Dark)
        {
            SetBrush(resources, "WindowBackgroundBrush", System.Windows.Media.Color.FromRgb(17, 24, 39));
            SetBrush(resources, "CardBackgroundBrush", System.Windows.Media.Color.FromRgb(31, 41, 55));
            SetBrush(resources, "PrimaryTextBrush", System.Windows.Media.Color.FromRgb(243, 244, 246));
            SetBrush(resources, "SecondaryTextBrush", System.Windows.Media.Color.FromRgb(156, 163, 175));
            SetBrush(resources, "BorderBrushStrong", System.Windows.Media.Color.FromRgb(55, 65, 81));
            SetBrush(resources, "AccentBrush", System.Windows.Media.Color.FromRgb(59, 130, 246));
            SetBrush(resources, "AccentBrushHover", System.Windows.Media.Color.FromRgb(37, 99, 235));
        }
        else
        {
            SetBrush(resources, "WindowBackgroundBrush", System.Windows.Media.Color.FromRgb(245, 246, 248));
            SetBrush(resources, "CardBackgroundBrush", System.Windows.Media.Color.FromRgb(255, 255, 255));
            SetBrush(resources, "PrimaryTextBrush", System.Windows.Media.Color.FromRgb(17, 24, 39));
            SetBrush(resources, "SecondaryTextBrush", System.Windows.Media.Color.FromRgb(107, 114, 128));
            SetBrush(resources, "BorderBrushStrong", System.Windows.Media.Color.FromRgb(209, 213, 219));
            SetBrush(resources, "AccentBrush", System.Windows.Media.Color.FromRgb(37, 99, 235));
            SetBrush(resources, "AccentBrushHover", System.Windows.Media.Color.FromRgb(29, 78, 216));
        }
    }

    private static void SetBrush(ResourceDictionary resources, string key, System.Windows.Media.Color color)
    {
        if (resources[key] is SolidColorBrush brush)
        {
            if (brush.IsFrozen)
            {
                resources[key] = new SolidColorBrush(color);
            }
            else
            {
                brush.Color = color;
            }
        }
        else
        {
            resources[key] = new SolidColorBrush(color);
        }
    }
}
