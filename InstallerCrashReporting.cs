using DataGateWin.CrashReporting;

namespace DataGateWin.Installer;

internal static class InstallerCrashReporting
{
    public const string BaseUrl = "https://api.datagateapp.com/";
    public const string ProcessName = "com.imkolganov.datagate.win.installer";

    public static CrashReportingConfiguration CreateConfiguration() =>
        new()
        {
            Enabled = true,
            BaseUrl = BaseUrl,
            ProcessName = ProcessName,
            CrashToken = ""
        };
}
