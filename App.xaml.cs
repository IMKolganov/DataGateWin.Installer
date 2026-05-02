using System.Windows;
using DataGateWin.Installer.Localization;

namespace DataGateWin.Installer;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        InstallerLanguageService.ApplyInitialLanguage();

        var args = e.Args ?? Array.Empty<string>();
        var uninstall = args.Any(a => string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase));
        if (!uninstall)
        {
            MainWindow = new MainWindow();
            MainWindow.Show();
            return;
        }

        var quiet = args.Any(a => string.Equals(a, "--quiet", StringComparison.OrdinalIgnoreCase));
        try
        {
            UninstallRunner.Execute(quiet);
            Shutdown();
        }
        catch (Exception ex)
        {
            if (!quiet)
            {
                System.Windows.MessageBox.Show(
                    ex.Message,
                    InstallerLoc.T("Install_UninstallFailedTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            Shutdown(1);
        }
    }
}
