using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DataGateWin.CrashReporting;
using DataGateWin.Installer.Localization;
using DataGateWin.Localization;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;

namespace DataGateWin.Installer;

public partial class MainWindow : Window
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/IMKolganov/DataGateWin/releases/latest";
    private const string AssetNamePrefix = "DataGateWin.v";
    private const string AssetNameSuffix = ".zip";

    private CancellationTokenSource? _cts;
    private readonly bool _isUpdateMode;
    private readonly string _installerDir;
    private InstallerWizardStep _currentStep;
    private bool _installCompleted;
    private string? _lastInstalledExePath;
    private readonly bool _suppressThemeChange;
    private bool _suppressInstallerLanguageCombo;
    private EventHandler? _installerLanguageChangedHandler;
    private readonly List<string> _logLines = new();

    private enum AppTheme
    {
        Light,
        Dark
    }

    public MainWindow()
    {
        InitializeComponent();

        var installerExe = Process.GetCurrentProcess().MainModule?.FileName;
        _installerDir = Path.GetDirectoryName(installerExe) ?? Environment.CurrentDirectory;

        var options = InstallerCommandLine.Parse(Environment.GetCommandLineArgs());
        _isUpdateMode = options.IsUpdateMode;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var registered = InstallRegistry.TryGetInstallLocation()?.Trim();
        InstallPathTextBox.Text = !string.IsNullOrWhiteSpace(registered)
            ? registered
            : Path.Combine(programFiles, "DataGate");
        UrlTextBox.Text = "latest (auto)";

        var systemTheme = GetSystemTheme();
        ApplyTheme(systemTheme);
        _suppressThemeChange = true;
        LightThemeRadioButton.IsChecked = systemTheme == AppTheme.Light;
        DarkThemeRadioButton.IsChecked = systemTheme == AppTheme.Dark;
        _suppressThemeChange = false;

        _currentStep = InstallerWizardRules.GetInitialStep(_isUpdateMode);

        _installerLanguageChangedHandler = (_, _) => Dispatcher.Invoke(PopulateInstallerLanguageCombo);
        InstallerLanguageService.LanguageChanged += _installerLanguageChangedHandler;
        Unloaded += (_, _) =>
        {
            if (_installerLanguageChangedHandler is not null)
                InstallerLanguageService.LanguageChanged -= _installerLanguageChangedHandler;
        };

        PopulateInstallerLanguageCombo();
        UpdateWizardUi();
    }

    private void PopulateInstallerLanguageCombo()
    {
        var pref = InstallerPreferenceStore.ReadUiLanguagePreference();
        var normalized = InstallerLanguageService.NormalizePreference(
            string.IsNullOrWhiteSpace(pref) ? InstallerLanguageService.SystemPreference : pref);

        _suppressInstallerLanguageCombo = true;
        InstallerLanguageCombo.Items.Clear();
        InstallerLanguageCombo.Items.Add(new ComboBoxItem
        {
            Tag = InstallerLanguageService.SystemPreference,
            Content = InstallerLanguageService.GetLanguageDisplayName(InstallerLanguageService.SystemPreference),
        });
        foreach (var code in UiLocale.GetLanguagePickerCodes())
        {
            InstallerLanguageCombo.Items.Add(new ComboBoxItem
            {
                Tag = code,
                Content = InstallerLanguageService.GetLanguageDisplayName(code),
            });
        }

        ComboBoxItem? match = null;
        foreach (ComboBoxItem item in InstallerLanguageCombo.Items)
        {
            if (item.Tag is string t && string.Equals(t, normalized, StringComparison.OrdinalIgnoreCase))
            {
                match = item;
                break;
            }
        }

        InstallerLanguageCombo.SelectedItem = match ?? InstallerLanguageCombo.Items[0] as ComboBoxItem;
        _suppressInstallerLanguageCombo = false;
    }

    private void InstallerLanguageCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressInstallerLanguageCombo)
            return;

        if (InstallerLanguageCombo.SelectedItem is not ComboBoxItem { Tag: string code })
            return;

        InstallerPreferenceStore.WriteUiLanguagePreference(code);
        InstallerLanguageService.Apply(code);
    }

    /// <summary>
    /// If this installer matches the installed build, asks launch vs continue vs exit.
    /// Returns true if the wizard should stop (launch or exit).
    /// </summary>
    private bool TryInterruptWizardForSameVersionInstalled()
    {
        var dir = InstallRegistry.TryGetInstallLocation()?.Trim();
        if (string.IsNullOrWhiteSpace(dir))
            return false;

        var exePath = Path.Combine(dir, InstallerConstants.ExeName);
        if (!File.Exists(exePath) || !InstalledBuild.IsSameAsInstaller(exePath))
            return false;

        var r = MessageBox.Show(
            InstallerLoc.T("Install_AlreadyInstalledBody"),
            InstallerLoc.T("Install_AlreadyInstalledTitle"),
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        switch (r)
        {
            case MessageBoxResult.Yes:
                try
                {
                    Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    CrashReporter.ReportNonFatal(ex, "Installer.LaunchInstalledApp");
                    MessageBox.Show(
                        ex.Message,
                        InstallerLoc.T("Msg_ErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }

                System.Windows.Application.Current.Shutdown();
                return true;

            case MessageBoxResult.Cancel:
                System.Windows.Application.Current.Shutdown();
                return true;

            default:
                return false;
        }
    }

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        if (!_isUpdateMode)
            return;

        try
        {
            InstallPathTextBox.Text = _installerDir;
            await StartInstallAsync(isUpdate: true);
        }
        catch (Exception ex)
        {
            CrashReporter.ReportNonFatal(ex, "Installer.UpdateModeStart");
            Log($"ERROR: {ex.Message}");
            MessageBox.Show(ex.ToString(), InstallerLoc.T("Install_UpdateFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new FolderBrowserDialog
        {
            Description = InstallerLoc.T("Install_SelectFolderTitle"),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        var result = dlg.ShowDialog();
        if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
        {
            InstallPathTextBox.Text = dlg.SelectedPath;
        }
    }

    private void PolicyCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateNextButtonState();
    }

    private void InstallPathTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateNextButtonState();
    }

    private void ThemeRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressThemeChange)
            return;

        var theme = LightThemeRadioButton.IsChecked == true ? AppTheme.Light : AppTheme.Dark;
        ApplyTheme(theme);
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        switch (_currentStep)
        {
            case InstallerWizardStep.Policy:
                if (PolicyCheckBox.IsChecked != true)
                    return;
                if (TryInterruptWizardForSameVersionInstalled())
                    return;
                _currentStep = InstallerWizardStep.Path;
                UpdateWizardUi();
                break;
            case InstallerWizardStep.Path:
                if (string.IsNullOrWhiteSpace(InstallPathTextBox.Text))
                    return;
                _currentStep = InstallerWizardStep.Install;
                UpdateWizardUi();
                await StartInstallAsync(isUpdate: false);
                break;
            case InstallerWizardStep.Install:
                if (!_installCompleted)
                    return;
                _currentStep = InstallerWizardStep.Finish;
                UpdateWizardUi();
                break;
            case InstallerWizardStep.Finish:
                if (LaunchCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(_lastInstalledExePath))
                {
                    if (File.Exists(_lastInstalledExePath))
                        Process.Start(new ProcessStartInfo(_lastInstalledExePath) { UseShellExecute = true });
                }
                Close();
                break;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            InstallerLoc.T("Install_CancelSetupPrompt"),
            InstallerLoc.T("Install_WindowTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
            Close();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CancelButton_Click(sender, e);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        DragMove();
    }

    private void UpdateWizardUi()
    {
        PolicyPanel.Visibility = _currentStep == InstallerWizardStep.Policy ? Visibility.Visible : Visibility.Collapsed;
        PathPanel.Visibility = _currentStep == InstallerWizardStep.Path ? Visibility.Visible : Visibility.Collapsed;
        InstallPanel.Visibility = _currentStep == InstallerWizardStep.Install ? Visibility.Visible : Visibility.Collapsed;
        FinishPanel.Visibility = _currentStep == InstallerWizardStep.Finish ? Visibility.Visible : Visibility.Collapsed;

        InstallTitleTextBlock.Text = _isUpdateMode
            ? InstallerLoc.T("Install_TitleUpdating")
            : InstallerLoc.T("Install_TitleInstalling");
        FinishStatusTextBlock.Text = _isUpdateMode
            ? InstallerLoc.T("Install_FinishBodyUpdated")
            : InstallerLoc.T("Install_FinishBodyInstalled");

        InstallPathTextBox.IsEnabled = !_isUpdateMode;
        BrowseButton.IsEnabled = !_isUpdateMode;

        NextButton.Content = _currentStep == InstallerWizardStep.Finish
            ? InstallerLoc.T("Install_Finish")
            : InstallerLoc.T("Install_Next");
        StepTextBlock.Text = _currentStep switch
        {
            InstallerWizardStep.Policy => InstallerLoc.T("Install_StepFmt", 1),
            InstallerWizardStep.Path => InstallerLoc.T("Install_StepFmt", 2),
            InstallerWizardStep.Install => InstallerLoc.T("Install_StepFmt", 3),
            InstallerWizardStep.Finish => InstallerLoc.T("Install_StepFmt", 4),
            _ => string.Empty
        };
        UpdateNextButtonState();
    }

    private void UpdateNextButtonState()
    {
        NextButton.IsEnabled = InstallerWizardRules.IsNextEnabled(
            _currentStep,
            PolicyCheckBox.IsChecked == true,
            InstallPathTextBox.Text,
            _installCompleted);
    }

    private async Task StartInstallAsync(bool isUpdate)
    {
        _installCompleted = false;
        UpdateNextButtonState();
        CancelButton.IsEnabled = false;
        UninstallButton.IsEnabled = false;

        DownloadProgressBar.Value = 0;
        InstallProgressBar.Value = 0;
        LogTextBox.Clear();

        _cts = new CancellationTokenSource();

        string? url = null;
        try
        {
            url = await ResolveLatestReleaseZipUrlAsync(_cts.Token);
            UrlTextBox.Text = url;
            Log($"Using release asset: {url}");

            var installDir = isUpdate ? _installerDir : InstallPathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(installDir))
                throw new InvalidOperationException("Install folder is empty.");

            await ProcessStopCoordinator.EnsureAppProcessesStoppedAsync(true, Log).ConfigureAwait(true);

            if (isUpdate)
            {
                installDir = InstallerOperations.ResolveUpdateInstallDir(installDir, InstallerConstants.ExeName, Log);
                InstallPathTextBox.Text = installDir;
            }

            Log($"{(isUpdate ? "Updating" : "Installing")} to: {installDir}");
            Directory.CreateDirectory(installDir);

            var tempRoot = Path.Combine(Path.GetTempPath(), "DataGateInstaller", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            var zipPath = Path.Combine(tempRoot, "package.zip");
            var extractDir = Path.Combine(tempRoot, "extract");

            try
            {
                await DownloadFileAsync(url, zipPath, new Progress<double>(p => SetDownloadProgress(p)), _cts.Token);

                Log("Extracting zip...");
                Directory.CreateDirectory(extractDir);
                ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);
                ReportInstallProgress(10);

                var installerExe = Process.GetCurrentProcess().MainModule?.FileName;
                var installerExePath = installerExe != null ? Path.GetFullPath(installerExe) : string.Empty;

                Log("Deploying files...");
                InstallerOperations.CopyDirectoryWithProgress(
                    extractDir,
                    installDir,
                    dest => !string.IsNullOrWhiteSpace(installerExePath) &&
                            string.Equals(Path.GetFullPath(dest), installerExePath, StringComparison.OrdinalIgnoreCase),
                    new Progress<double>(p => ReportInstallProgress(10 + (p * 0.9))),
                    _cts.Token);

                var exePath = Path.Combine(installDir, InstallerConstants.ExeName);
                if (!File.Exists(exePath))
                    throw new FileNotFoundException("Main executable was not found after extraction.", exePath);

                if (!isUpdate)
                {
                    Log("Creating shortcuts...");
                    ShortcutHelper.RefreshShortcuts(
                        installDir,
                        exePath,
                        startMenu: StartMenuShortcutCheckBox.IsChecked == true,
                        desktop: DesktopShortcutCheckBox.IsChecked == true);
                }
                else
                {
                    Log("Refreshing shortcuts (update)...");
                    ShortcutHelper.RefreshShortcuts(
                        installDir,
                        exePath,
                        startMenu: true,
                        desktop: ShortcutHelper.LegacyOrCurrentDesktopShortcutExists());
                }

                Log("Registering Apps & Features entry...");
                InstallRegistry.RegisterUninstallEntry(installDir);

                Log("Registering App Paths...");
                InstallRegistry.RegisterAppPaths(exePath, installDir);

                ReportInstallProgress(100);
                _lastInstalledExePath = exePath;
                _installCompleted = true;
                UpdateNextButtonState();

                Log("Done.");
            }
            finally
            {
                try
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
                catch (Exception ex)
                {
                    CrashReporter.ReportNonFatal(ex, "Installer.CleanupTempRoot");
                    Debug.WriteLine(ex);
                }
            }
        }
        catch (Exception ex)
        {
            CrashReporter.ReportNonFatal(ex, isUpdate ? "Installer.UpdateFailed" : "Installer.InstallFailed");

            if (!string.IsNullOrWhiteSpace(url))
                Log($"ERROR: failed to download from: {url}");
            Log($"ERROR: {ex.Message}");
            MessageBox.Show(
                ex.ToString(),
                isUpdate ? InstallerLoc.T("Install_UpdateFailedTitle") : InstallerLoc.T("Install_InstallFailedTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            CancelButton.IsEnabled = true;
            UninstallButton.IsEnabled = true;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            InstallerLoc.T("Install_ConfirmUninstallFmt", InstallerConstants.ProductName),
            InstallerLoc.T("Install_ConfirmUninstallTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            LogTextBox.Clear();
            var regDir = InstallRegistry.TryGetInstallLocation()?.Trim();
            var fallbackDir = InstallPathTextBox.Text.Trim();
            var labelDir = !string.IsNullOrWhiteSpace(regDir) ? regDir : fallbackDir;
            Log($"Uninstalling from: {labelDir}");

            await UninstallRunner.ExecuteAsync(false, fallbackDir, Log).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            CrashReporter.ReportNonFatal(ex, "Installer.UninstallButton");
            Log($"ERROR: {ex.Message}");
            MessageBox.Show(ex.ToString(), InstallerLoc.T("Install_UninstallFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static async Task DownloadFileAsync(string url, string destinationPath, IProgress<double> progress, CancellationToken ct)
    {
        using var http = new HttpClient();
        await InstallerDownloader.DownloadFileAsync(http, url, destinationPath, progress, ct);
    }

    private static async Task<string> ResolveLatestReleaseZipUrlAsync(CancellationToken ct)
    {
        using var http = InstallerDownloader.CreateGitHubHttpClient();
        return await InstallerDownloader.ResolveLatestReleaseZipUrlAsync(
            http,
            LatestReleaseApiUrl,
            AssetNamePrefix,
            AssetNameSuffix,
            ct);
    }

    private void Log(string message)
    {
        InstallerUiThread.Run(() => AppendLogLine(message));
    }

    private void AppendLogLine(string message)
    {
        var line = $"[{DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}] {message}";
        var dropped = InMemoryLogBudget.AppendLine(_logLines, line);
        if (dropped)
            LogTextBox.Text = InMemoryLogBudget.JoinLinesForTextBox(_logLines);
        else
            LogTextBox.AppendText(line + Environment.NewLine);

        LogTextBox.ScrollToEnd();
    }

    private void SetDownloadProgress(double value)
    {
        InstallerUiThread.Run(() =>
            DownloadProgressBar.Value = Math.Clamp(value, 0, 100));
    }

    private void ReportInstallProgress(double value)
    {
        InstallerUiThread.Run(() =>
            InstallProgressBar.Value = Math.Clamp(value, 0, 100));
    }

    private AppTheme GetSystemTheme()
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

    private void ApplyTheme(AppTheme mode)
    {
        if (mode == AppTheme.Dark)
        {
            SetBrush("WindowBackgroundBrush", System.Windows.Media.Color.FromRgb(17, 24, 39));
            SetBrush("CardBackgroundBrush", System.Windows.Media.Color.FromRgb(31, 41, 55));
            SetBrush("PrimaryTextBrush", System.Windows.Media.Color.FromRgb(243, 244, 246));
            SetBrush("SecondaryTextBrush", System.Windows.Media.Color.FromRgb(156, 163, 175));
            SetBrush("BorderBrushStrong", System.Windows.Media.Color.FromRgb(55, 65, 81));
            SetBrush("AccentBrush", System.Windows.Media.Color.FromRgb(59, 130, 246));
            SetBrush("AccentBrushHover", System.Windows.Media.Color.FromRgb(37, 99, 235));
        }
        else
        {
            SetBrush("WindowBackgroundBrush", System.Windows.Media.Color.FromRgb(245, 246, 248));
            SetBrush("CardBackgroundBrush", System.Windows.Media.Color.FromRgb(255, 255, 255));
            SetBrush("PrimaryTextBrush", System.Windows.Media.Color.FromRgb(17, 24, 39));
            SetBrush("SecondaryTextBrush", System.Windows.Media.Color.FromRgb(107, 114, 128));
            SetBrush("BorderBrushStrong", System.Windows.Media.Color.FromRgb(209, 213, 219));
            SetBrush("AccentBrush", System.Windows.Media.Color.FromRgb(37, 99, 235));
            SetBrush("AccentBrushHover", System.Windows.Media.Color.FromRgb(29, 78, 216));
        }
    }

    private void SetBrush(string key, System.Windows.Media.Color color)
    {
        if (Resources[key] is SolidColorBrush brush)
        {
            if (brush.IsFrozen)
            {
                Resources[key] = new SolidColorBrush(color);
            }
            else
            {
                brush.Color = color;
            }
        }
        else
        {
            Resources[key] = new SolidColorBrush(color);
        }
    }

}
