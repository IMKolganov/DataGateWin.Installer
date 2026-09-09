using System.IO;
using System.Windows;
using DataGateWin.CrashReporting;
using DataGateWin.Installer.Localization;

namespace DataGateWin.Installer;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ConfigureCrashReporting();
        InstallCrashReportingHandlers();
        _ = CrashReporter.FlushPendingAsync(CancellationToken.None);

        base.OnStartup(e);

        InstallerLanguageService.ApplyInitialLanguage();

        var options = InstallerCommandLine.Parse(e.Args ?? Array.Empty<string>());
        if (!options.IsUninstall)
        {
            if (options.Quiet)
            {
                try
                {
                    var installerExe = Environment.ProcessPath
                        ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    var installerDir = Path.GetDirectoryName(installerExe) ?? Environment.CurrentDirectory;
                    var package = LocalBuildPackage.TryFind(installerDir)
                        ?? throw new DirectoryNotFoundException(
                            "Quiet install requires a DataGateWinBuild* folder next to the installer (or its parent) with DataGateWin.exe.");
                    var installDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        "DataGate");
                    QuietLocalInstallRunner.Execute(package, installDir);
                    Shutdown(0);
                }
                catch (Exception ex)
                {
                    ReportStartupFailureBeforeShutdown(ex);
                    Shutdown(1);
                }

                return;
            }

            MainWindow = new MainWindow();
            MainWindow.Show();
            return;
        }

        try
        {
            UninstallRunner.Execute(options.Quiet);
            Shutdown();
        }
        catch (Exception ex)
        {
            ReportStartupFailureBeforeShutdown(ex);

            if (!options.Quiet)
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

    private static void ReportStartupFailureBeforeShutdown(Exception exception)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            CrashReporter.ReportNonFatalAsync(exception, "Installer.UninstallStartup", cts.Token)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            // Do not block the uninstall exit path if crash reporting itself fails.
        }
    }

    private void InstallCrashReportingHandlers()
    {
        CrashReporter.InstallDomainHandlers();
        DispatcherUnhandledException += (_, args) =>
        {
            CrashReporter.HandleDispatcherUnhandled(args.Exception);
        };
    }

    private static void ConfigureCrashReporting()
    {
        CrashReporter.Configure(InstallerCrashReporting.CreateConfiguration());
    }
}
