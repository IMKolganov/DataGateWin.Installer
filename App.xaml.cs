using System.IO;
using System.Windows;
using DataGateWin.CrashReporting;
using DataGateWin.Installer.Localization;

namespace DataGateWin.Installer;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        ConfigureCrashReporting();
        InstallCrashReportingHandlers();

        base.OnStartup(e);

        InstallerLanguageService.ApplyInitialLanguage();

        var options = InstallerCommandLine.Parse(e.Args ?? Array.Empty<string>());
        if (!options.IsUninstall)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var recoveryLog = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DataGateWin", "installer-network-recovery.log");
            await Task.Run(() => StartupDnsRecovery.Run(line =>
            {
                System.Diagnostics.Debug.WriteLine(line);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(recoveryLog)!);
                    File.AppendAllText(recoveryLog, $"{DateTimeOffset.Now:O} {line}{Environment.NewLine}");
                }
                catch { /* logging must not prevent recovery */ }
            }));
            _ = CrashReporter.FlushPendingAsync(CancellationToken.None);
            if (options.Quiet)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                try
                {
                    if (options.IsUpdateMode)
                        throw new InvalidOperationException("Quiet update is not supported; use the interactive update flow.");
                    var installerExe = Environment.ProcessPath
                        ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    var installerDir = Path.GetDirectoryName(installerExe) ?? Environment.CurrentDirectory;
                    var package = LocalBuildPackage.TryFind(installerDir)
                        ?? throw new DirectoryNotFoundException(
                            "Quiet install requires a DataGateWinBuild* folder next to the installer (or its parent) with DataGateWin.exe.");
                    var installDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        "DataGate");
                    await QuietLocalInstallRunner.ExecuteAsync(package, installDir);
                    Shutdown(0);
                }
                catch (Exception ex)
                {
                    ReportStartupFailureBeforeShutdown(ex);
                    Shutdown(1);
                }

                return;
            }

            ShutdownMode = ShutdownMode.OnLastWindowClose;
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
