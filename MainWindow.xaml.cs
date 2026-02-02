using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;

namespace DataGateWin.Installer;

public partial class MainWindow : Window
{
    private const string ProductName = "DataGate OpenVPN 3";
    private const string Publisher = "DataGate";
    private const string ExeName = "DataGateWin.exe";
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/IMKolganov/DataGateWin/releases/latest";
    private const string AssetNamePrefix = "DataGateWin.v";
    private const string AssetNameSuffix = ".zip";

    private const string UninstallRegKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\DataGateOpenVPN3";
    private const string AppPathsRegKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\DataGateWin.exe";

    private CancellationTokenSource? _cts;
    private readonly bool _isUpdateMode;
    private readonly string _installerDir;
    private WizardStep _currentStep;
    private bool _installCompleted;
    private string? _lastInstalledExePath;
    private readonly bool _suppressThemeChange;

    private enum WizardStep
    {
        Policy,
        Path,
        Install,
        Finish
    }

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

        var args = Environment.GetCommandLineArgs();
        _isUpdateMode = args.Any(a =>
            string.Equals(a, "update", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "--update", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "/update", StringComparison.OrdinalIgnoreCase));

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        InstallPathTextBox.Text = Path.Combine(programFiles, "DataGate");
        UrlTextBox.Text = "latest (auto)";

        var systemTheme = GetSystemTheme();
        ApplyTheme(systemTheme);
        _suppressThemeChange = true;
        LightThemeRadioButton.IsChecked = systemTheme == AppTheme.Light;
        DarkThemeRadioButton.IsChecked = systemTheme == AppTheme.Dark;
        _suppressThemeChange = false;

        _currentStep = _isUpdateMode ? WizardStep.Install : WizardStep.Policy;
        UpdateWizardUi();
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
            Log($"ERROR: {ex.Message}");
            MessageBox.Show(ex.ToString(), "Update failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new FolderBrowserDialog
        {
            Description = "Select installation folder",
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
            case WizardStep.Policy:
                if (PolicyCheckBox.IsChecked != true)
                    return;
                _currentStep = WizardStep.Path;
                UpdateWizardUi();
                break;
            case WizardStep.Path:
                if (string.IsNullOrWhiteSpace(InstallPathTextBox.Text))
                    return;
                _currentStep = WizardStep.Install;
                UpdateWizardUi();
                await StartInstallAsync(isUpdate: false);
                break;
            case WizardStep.Install:
                if (!_installCompleted)
                    return;
                _currentStep = WizardStep.Finish;
                UpdateWizardUi();
                break;
            case WizardStep.Finish:
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
        var result = MessageBox.Show("Are you sure you want to cancel setup?", ProductName,
            MessageBoxButton.YesNo, MessageBoxImage.Question);
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
        PolicyPanel.Visibility = _currentStep == WizardStep.Policy ? Visibility.Visible : Visibility.Collapsed;
        PathPanel.Visibility = _currentStep == WizardStep.Path ? Visibility.Visible : Visibility.Collapsed;
        InstallPanel.Visibility = _currentStep == WizardStep.Install ? Visibility.Visible : Visibility.Collapsed;
        FinishPanel.Visibility = _currentStep == WizardStep.Finish ? Visibility.Visible : Visibility.Collapsed;

        InstallTitleTextBlock.Text = _isUpdateMode ? "Updating" : "Installing";
        FinishStatusTextBlock.Text = _isUpdateMode
            ? "DataGateWin has been updated on your computer."
            : "DataGateWin has been installed on your computer.";

        InstallPathTextBox.IsEnabled = !_isUpdateMode;
        BrowseButton.IsEnabled = !_isUpdateMode;

        NextButton.Content = _currentStep == WizardStep.Finish ? "Finish" : "Next";
        StepTextBlock.Text = _currentStep switch
        {
            WizardStep.Policy => "Step 1 of 4",
            WizardStep.Path => "Step 2 of 4",
            WizardStep.Install => "Step 3 of 4",
            WizardStep.Finish => "Step 4 of 4",
            _ => string.Empty
        };
        UpdateNextButtonState();
    }

    private void UpdateNextButtonState()
    {
        NextButton.IsEnabled = _currentStep switch
        {
            WizardStep.Policy => PolicyCheckBox.IsChecked == true,
            WizardStep.Path => !string.IsNullOrWhiteSpace(InstallPathTextBox.Text),
            WizardStep.Install => _installCompleted,
            WizardStep.Finish => true,
            _ => false
        };
    }

    private async Task StartInstallAsync(bool isUpdate)
    {
        _installCompleted = false;
        UpdateNextButtonState();
        CancelButton.IsEnabled = false;

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

            if (isUpdate)
            {
                var targetExe = Path.Combine(installDir, ExeName);
                if (!File.Exists(targetExe))
                    throw new FileNotFoundException("DataGateWin.exe was not found next to the installer.", targetExe);
            }

            Log($"{(isUpdate ? "Updating" : "Installing")} to: {installDir}");
            Directory.CreateDirectory(installDir);

            var tempRoot = Path.Combine(Path.GetTempPath(), "DataGateInstaller", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            var zipPath = Path.Combine(tempRoot, "package.zip");
            var extractDir = Path.Combine(tempRoot, "extract");

            try
            {
                await DownloadFileAsync(url, zipPath, new Progress<double>(p => DownloadProgressBar.Value = p), _cts.Token);

                Log("Extracting zip...");
                Directory.CreateDirectory(extractDir);
                ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);
                ReportInstallProgress(10);

                var installerExe = Process.GetCurrentProcess().MainModule?.FileName;
                var installerExePath = installerExe != null ? Path.GetFullPath(installerExe) : string.Empty;

                Log("Deploying files...");
                CopyDirectoryWithProgress(
                    extractDir,
                    installDir,
                    dest => !string.IsNullOrWhiteSpace(installerExePath) &&
                            string.Equals(Path.GetFullPath(dest), installerExePath, StringComparison.OrdinalIgnoreCase),
                    new Progress<double>(p => ReportInstallProgress(10 + (p * 0.9))),
                    _cts.Token);

                var exePath = Path.Combine(installDir, ExeName);
                if (!File.Exists(exePath))
                    throw new FileNotFoundException("Main executable was not found after extraction.", exePath);

                if (!isUpdate)
                {
                    Log("Creating Start Menu shortcut...");
                    CreateStartMenuShortcut(installDir, exePath);

                    Log("Registering Apps & Features entry...");
                    RegisterUninstallEntry(installDir);

                    Log("Registering App Paths...");
                    RegisterAppPaths(exePath, installDir);
                }

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
                catch(Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(url))
                Log($"ERROR: failed to download from: {url}");
            Log($"ERROR: {ex.Message}");
            MessageBox.Show(ex.ToString(), isUpdate ? "Update failed" : "Install failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            CancelButton.IsEnabled = true;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var installDir = InstallPathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(installDir))
                throw new InvalidOperationException("Install folder is empty.");

            LogTextBox.Clear();
            Log($"Uninstalling from: {installDir}");

            Log("Removing Start Menu shortcut...");
            RemoveStartMenuShortcut();

            Log("Removing registry entries...");
            UnregisterUninstallEntry();
            UnregisterAppPaths();

            if (Directory.Exists(installDir))
            {
                Log("Deleting files...");
                Directory.Delete(installDir, recursive: true);
            }

            Log("Done.");
            MessageBox.Show("Uninstalled successfully.", ProductName, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log($"ERROR: {ex.Message}");
            MessageBox.Show(ex.ToString(), "Uninstall failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static async Task DownloadFileAsync(string url, string destinationPath, IProgress<double> progress, CancellationToken ct)
    {
        using var http = new HttpClient();
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;

        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = File.Create(destinationPath);

        var buffer = new byte[81920];
        long readTotal = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read == 0)
                break;

            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            readTotal += read;

            if (total.HasValue && total.Value > 0)
            {
                var pct = (double)readTotal / total.Value * 100.0;
                progress.Report(Math.Min(100.0, pct));
            }
        }

        if (!total.HasValue)
            progress.Report(100.0);
    }

    private static async Task<string> ResolveLatestReleaseZipUrlAsync(CancellationToken ct)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("DataGateWin.Installer");

        using var response = await http.GetAsync(LatestReleaseApiUrl, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("GitHub latest release response does not contain assets.");

        foreach (var asset in assets.EnumerateArray())
        {
            if (!asset.TryGetProperty("name", out var nameProp) || nameProp.ValueKind != JsonValueKind.String)
                continue;
            var name = nameProp.GetString() ?? string.Empty;
            if (!name.StartsWith(AssetNamePrefix, StringComparison.OrdinalIgnoreCase) ||
                !name.EndsWith(AssetNameSuffix, StringComparison.OrdinalIgnoreCase))
                continue;

            if (asset.TryGetProperty("browser_download_url", out var urlProp) &&
                urlProp.ValueKind == JsonValueKind.String)
            {
                var url = urlProp.GetString();
                if (!string.IsNullOrWhiteSpace(url))
                    return url;
            }
        }

        throw new InvalidOperationException($"No ZIP asset found matching {AssetNamePrefix}*{AssetNameSuffix}.");
    }

    private static void CopyDirectory(string sourceDir, string destinationDir, Func<string, bool>? skipDestination = null)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destFile = Path.Combine(destinationDir, relative);

            if (skipDestination?.Invoke(destFile) == true)
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            File.Copy(file, destFile, overwrite: true);
        }
    }

    private static string GetStartMenuFolder()
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
        return Path.Combine(programs, "DataGate");
    }

    private static void CreateStartMenuShortcut(string installDir, string exePath)
    {
        var folder = GetStartMenuFolder();
        Directory.CreateDirectory(folder);

        var shortcutPath = Path.Combine(folder, $"{ProductName}.lnk");

        // Uses WScript.Shell COM. Add COM reference:
        // "Windows Script Host Object Model" (IWshRuntimeLibrary)
        var shellType = Type.GetTypeFromProgID("WScript.Shell")!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = exePath;
        shortcut.WorkingDirectory = installDir;
        shortcut.WindowStyle = 1;
        shortcut.Description = ProductName;

        var iconPath = Path.Combine(installDir, "favicon.ico");
        if (File.Exists(iconPath))
            shortcut.IconLocation = iconPath;

        shortcut.Save();
    }

    private static void RemoveStartMenuShortcut()
    {
        var folder = GetStartMenuFolder();
        var shortcutPath = Path.Combine(folder, $"{ProductName}.lnk");

        if (File.Exists(shortcutPath))
            File.Delete(shortcutPath);

        if (Directory.Exists(folder) && Directory.GetFiles(folder).Length == 0 && Directory.GetDirectories(folder).Length == 0)
            Directory.Delete(folder);
    }

    private void RegisterUninstallEntry(string installDir)
    {
        var installerExe = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(installerExe))
            throw new InvalidOperationException("Cannot resolve installer path.");

        using var key = Registry.LocalMachine.CreateSubKey(UninstallRegKeyPath, writable: true);
        if (key == null)
            throw new InvalidOperationException("Failed to open HKLM uninstall key. Run as administrator.");

        key.SetValue("DisplayName", ProductName, RegistryValueKind.String);
        key.SetValue("Publisher", Publisher, RegistryValueKind.String);
        key.SetValue("InstallLocation", installDir, RegistryValueKind.String);

        var version = GetInstallerVersion();
        if (!string.IsNullOrWhiteSpace(version))
            key.SetValue("DisplayVersion", version, RegistryValueKind.String);

        key.SetValue("UninstallString", $"\"{installerExe}\" --uninstall", RegistryValueKind.String);
        key.SetValue("QuietUninstallString", $"\"{installerExe}\" --uninstall --quiet", RegistryValueKind.String);

        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void UnregisterUninstallEntry()
    {
        try { Registry.LocalMachine.DeleteSubKeyTree(UninstallRegKeyPath, throwOnMissingSubKey: false); }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private static void RegisterAppPaths(string exePath, string installDir)
    {
        using var key = Registry.LocalMachine.CreateSubKey(AppPathsRegKeyPath, writable: true);
        if (key == null)
            throw new InvalidOperationException("Failed to open HKLM App Paths key. Run as administrator.");

        key.SetValue(string.Empty, exePath, RegistryValueKind.String);
        key.SetValue("Path", installDir, RegistryValueKind.String);
    }

    private static void UnregisterAppPaths()
    {
        try { Registry.LocalMachine.DeleteSubKeyTree(AppPathsRegKeyPath, throwOnMissingSubKey: false); }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private static string? GetInstallerVersion()
    {
        var asm = typeof(MainWindow).Assembly;
        var v = asm.GetName().Version;
        return v?.ToString();
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}] {message}";
        LogTextBox.AppendText(line + Environment.NewLine);
        LogTextBox.ScrollToEnd();
    }

    private void ReportInstallProgress(double value)
    {
        InstallProgressBar.Value = Math.Clamp(value, 0, 100);
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

    private static void CopyDirectoryWithProgress(
        string sourceDir,
        string destinationDir,
        Func<string, bool>? skipDestination,
        IProgress<double> progress,
        CancellationToken ct)
    {
        Directory.CreateDirectory(destinationDir);

        var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        var copyList = new List<(string Source, string Destination)>();

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destFile = Path.Combine(destinationDir, relative);

            if (skipDestination?.Invoke(destFile) == true)
                continue;

            copyList.Add((file, destFile));
        }

        if (copyList.Count == 0)
        {
            progress.Report(100);
            return;
        }

        for (var i = 0; i < copyList.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (source, dest) = copyList[i];
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(source, dest, overwrite: true);
            progress.Report((i + 1) * 100.0 / copyList.Count);
        }
    }
}
