using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Windows;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;

namespace DataGateWin.Installer;

public partial class MainWindow : Window
{
    private const string ProductName = "DataGate OpenVPN 3";
    private const string Publisher = "DataGate";
    private const string ExeName = "DataGateWin.exe";

    private const string UninstallRegKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\DataGateOpenVPN3";
    private const string AppPathsRegKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\DataGateWin.exe";

    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        InstallPathTextBox.Text = Path.Combine(programFiles, "DataGate");
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog
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

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;
        UninstallButton.IsEnabled = false;
        BrowseButton.IsEnabled = false;

        ProgressBar.Value = 0;
        LogTextBox.Clear();

        _cts = new CancellationTokenSource();

        try
        {
            var url = UrlTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("Zip URL is empty.");

            var installDir = InstallPathTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(installDir))
                throw new InvalidOperationException("Install folder is empty.");

            Log($"Installing to: {installDir}");
            Directory.CreateDirectory(installDir);

            var tempRoot = Path.Combine(Path.GetTempPath(), "DataGateInstaller", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            var zipPath = Path.Combine(tempRoot, "package.zip");
            var extractDir = Path.Combine(tempRoot, "extract");

            try
            {
                await DownloadFileAsync(url, zipPath, new Progress<double>(p => ProgressBar.Value = p), _cts.Token);

                Log("Extracting zip...");
                Directory.CreateDirectory(extractDir);
                ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

                Log("Deploying files...");
                CopyDirectory(extractDir, installDir);

                var exePath = Path.Combine(installDir, ExeName);
                if (!File.Exists(exePath))
                    throw new FileNotFoundException("Main executable was not found after extraction.", exePath);

                Log("Creating Start Menu shortcut...");
                CreateStartMenuShortcut(installDir, exePath);

                Log("Registering Apps & Features entry...");
                RegisterUninstallEntry(installDir);

                Log("Registering App Paths...");
                RegisterAppPaths(exePath, installDir);

                ProgressBar.Value = 100;
                Log("Done.");

                var launch = MessageBox.Show("Installed successfully. Launch application now?", ProductName,
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (launch == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                }
            }
            finally
            {
                try { Directory.Delete(tempRoot, recursive: true); } catch { }
            }
        }
        catch (Exception ex)
        {
            Log($"ERROR: {ex.Message}");
            MessageBox.Show(ex.ToString(), "Install failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            InstallButton.IsEnabled = true;
            UninstallButton.IsEnabled = true;
            BrowseButton.IsEnabled = true;
            _cts.Dispose();
            _cts = null;
        }
    }

    private void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var installDir = InstallPathTextBox.Text?.Trim();
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

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destFile = Path.Combine(destinationDir, relative);

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
        catch { }
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
        catch { }
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
}
