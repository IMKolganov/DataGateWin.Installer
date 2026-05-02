using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Windows;
using DataGateWin.Localization;

namespace DataGateWin.Installer.Localization;

internal static class InstallerLanguageService
{
    public static event EventHandler? LanguageChanged;

    private static ResourceDictionary? _stringsBase;
    private static ResourceDictionary? _stringsOverlay;
    private static ResourceDictionary? _extras;

    public const string SystemPreference = "system";

    public static void ApplyInitialLanguage()
    {
        var stored = InstallerPreferenceStore.ReadUiLanguagePreference();
        var pref = string.IsNullOrWhiteSpace(stored) ? SystemPreference : stored.Trim().ToLowerInvariant();
        Apply(pref);
    }

    public static void Apply(string preference)
    {
        preference = NormalizePreference(preference);
        var effective = ResolveEffective(preference);

        try
        {
            var loc = UiLocale.FindByCode(effective);
            var ci = loc != null
                ? CultureInfo.GetCultureInfo(loc.CultureName)
                : CultureInfo.GetCultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = ci;
            CultureInfo.DefaultThreadCurrentCulture = ci;
        }
        catch (CultureNotFoundException)
        {
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
        }

        var app = System.Windows.Application.Current;
        if (app is null)
            return;

        var merged = app.Resources.MergedDictionaries;
        Remove(ref _stringsBase, merged);
        Remove(ref _stringsOverlay, merged);
        Remove(ref _extras, merged);

        var asmName = Assembly.GetExecutingAssembly().GetName().Name!;
        _stringsBase = LoadDic(asmName, "Localization/Strings.en.xaml");
        merged.Add(_stringsBase);

        if (!string.Equals(effective, "en", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                _stringsOverlay = LoadDic(asmName, $"Localization/Strings.{effective}.xaml");
                merged.Add(_stringsOverlay);
            }
            catch
            {
                _stringsOverlay = null;
            }
        }

        _extras = LoadDic(asmName, "Localization/InstallerExtras.en.xaml");
        merged.Add(_extras);

        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    static ResourceDictionary LoadDic(string asmName, string relativePath)
    {
        var uri = new Uri($"/{asmName};component/{relativePath}", UriKind.Relative);
        return new ResourceDictionary { Source = uri };
    }

    static void Remove(ref ResourceDictionary? d, IList<ResourceDictionary> merged)
    {
        if (d is null)
            return;
        merged.Remove(d);
        d = null;
    }

    public static string NormalizePreference(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return SystemPreference;

        var s = languageCode.Trim().ToLowerInvariant();
        if (s is "system" or "auto" or "default" or "os")
            return SystemPreference;

        if (UiLocale.All.Any(l => string.Equals(l.Code, s, StringComparison.OrdinalIgnoreCase)))
            return s;

        return SystemPreference;
    }

    public static string ResolveEffective(string preference)
    {
        if (preference == SystemPreference)
            return CultureMapping.MapCultureToSupportedCode(CultureInfo.CurrentUICulture);

        return preference;
    }

    public static string GetLanguageDisplayName(string code)
    {
        if (string.Equals(code, SystemPreference, StringComparison.OrdinalIgnoreCase))
        {
            if (System.Windows.Application.Current?.TryFindResource("Lang_Name_system") is string sys && !string.IsNullOrWhiteSpace(sys))
                return sys;
            return "Same as Windows display language";
        }

        var loc = UiLocale.FindByCode(code);
        if (loc is null)
            return code;
        try
        {
            return CultureInfo.GetCultureInfo(loc.CultureName).NativeName;
        }
        catch (CultureNotFoundException)
        {
            return code;
        }
    }
}
