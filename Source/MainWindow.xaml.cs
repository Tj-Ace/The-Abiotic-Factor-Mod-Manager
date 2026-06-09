using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace AbioticLoader;

public partial class MainWindow : Window
{
    private const string CurrentModLoaderVersion = "0.3";
    private readonly AppStateService _stateService = new();
    private readonly ModLibraryService _modLibraryService = new();
    private readonly GameInstallService _gameInstallService = new();
    private readonly AppState _state;

    public MainWindow()
    {
        InitializeComponent();

        _state = _stateService.Load();
        ResolveInstallPath();
        Icon = BitmapFrame.Create(new Uri(Path.Combine(AppPaths.AssetsDirectory, "Gate Logo Pink.ico"), UriKind.Absolute));
        NavigateHome();
        Loaded += (_, _) =>
        {
            ShowUe4ssReminderIfNeeded();
            CheckModLoaderUpdatesOnStartup();
        };

        Closing += (_, _) =>
        {
            _state.UiSequenceNumber = _state.UiSequenceNumber < 0 ? 0 : _state.UiSequenceNumber + 1;
            _stateService.Save(_state);
            AppLogger.Info("Main window closing.");
        };
    }

    private void ResolveInstallPath()
    {
        if (!string.IsNullOrWhiteSpace(_state.GameInstallPath) && Directory.Exists(_state.GameInstallPath))
        {
            AppLogger.Info($"Using saved Abiotic Factor install path: {_state.GameInstallPath}.");
            return;
        }

        var detectedPath = _gameInstallService.DetectInstallPath();
        if (string.IsNullOrWhiteSpace(detectedPath))
        {
            AppLogger.Warn("No Abiotic Factor install path could be detected at startup.");
            ShowMissingInstallWarningIfNeeded();
            return;
        }

        _state.GameInstallPath = detectedPath;
        _stateService.Save(_state);
        AppLogger.Info($"Saved detected Abiotic Factor install path: {_state.GameInstallPath}.");
    }

    private void ShowMissingInstallWarningIfNeeded()
    {
        if (_state.SawMissingInstallWarning)
        {
            return;
        }

        MessageBox.Show(
            this,
            "Abiotic Factor could not be found.\n\nPlease set the install path in Settings.",
            "Abiotic Loader",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        _state.SawMissingInstallWarning = true;
        _stateService.Save(_state);
        AppLogger.Info("Displayed missing install warning.");
    }

    private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            AppLogger.Info("Drag bar double-click ignored.");
            return;
        }

        AppLogger.Info("Window drag initiated.");
        DragMove();
    }

    private void NavigateHome()
    {
        try
        {
            AppLogger.Info("Navigating to home view.");
            ViewHost.Content = new HomeView(LaunchGame, OpenUe4ss, OpenMods, OpenSettings, OpenCredits, BrowseMods, ExitApplication, _state, _modLibraryService);
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to navigate to home view.", exception);
            Close();
        }
    }

    private void ShowUe4ssReminderIfNeeded()
    {
        if (_state.SawUe4ssWarning)
        {
            return;
        }

        MessageBox.Show(
            this,
            "Check the UE4SS page before using mods so the loader can keep the UE4SS package current.",
            "UE4SS Reminder",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        _state.SawUe4ssWarning = true;
        _stateService.Save(_state);
        AppLogger.Info("Displayed one-time UE4SS reminder at startup.");
    }

    private void CheckModLoaderUpdatesOnStartup()
    {
        if (!_state.AutoCheckModLoaderUpdates)
        {
            AppLogger.Info("Automatic mod loader update check is disabled.");
            return;
        }

        try
        {
            var latestVersion = _gameInstallService.GetLatestModLoaderVersion();
            if (latestVersion is null)
            {
                AppLogger.Info("Automatic mod loader update check found no published version.");
                return;
            }

            var currentVersion = new Version(CurrentModLoaderVersion);
            if (latestVersion <= currentVersion)
            {
                AppLogger.Info($"Automatic mod loader update check found no newer version. Current {CurrentModLoaderVersion}, latest {latestVersion}.");
                return;
            }

            var latestVersionText = latestVersion.ToString();
            if (string.Equals(_state.LastNotifiedModLoaderVersion, latestVersionText, StringComparison.OrdinalIgnoreCase))
            {
                AppLogger.Info($"New mod loader version {latestVersionText} already notified.");
                return;
            }

            MessageBox.Show(
                this,
                $"A newer mod loader version was found.\n\nCurrent: {CurrentModLoaderVersion}\nLatest: {latestVersionText}",
                "Mod Loader Update",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            _state.LastNotifiedModLoaderVersion = latestVersionText;
            _stateService.Save(_state);
            AppLogger.Info($"Displayed mod loader update warning for version {latestVersionText}.");
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Automatic mod loader update check failed.", exception);
        }
    }

    private void LaunchGame()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_state.GameInstallPath) || !Directory.Exists(_state.GameInstallPath))
            {
                ResolveInstallPath();
            }

            if (string.IsNullOrWhiteSpace(_state.GameInstallPath))
            {
                AppLogger.Warn("Launch requested but no Abiotic Factor install path is available.");
                return;
            }

            var executablePath = _gameInstallService.GetLaunchExecutablePath(_state.GameInstallPath);
            if (!File.Exists(executablePath))
            {
                AppLogger.Warn($"Launch executable missing: {executablePath}.");
                return;
            }

            AppLogger.Info($"Launching game from {executablePath}.");
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? _state.GameInstallPath,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to launch Abiotic Factor.", exception);
        }
    }

    private void OpenMods()
    {
        try
        {
            AppLogger.Info("Opening mods view.");
            ViewHost.Content = new ModsView(NavigateHome, _state, _modLibraryService, _stateService);
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to open mods view.", exception);
            Close();
        }
    }

    private void OpenCredits()
    {
        try
        {
            AppLogger.Info("Opening credits view.");
            ViewHost.Content = new CreditsView(NavigateHome);
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to open credits view.", exception);
            Close();
        }
    }

    private void OpenSettings()
    {
        try
        {
            AppLogger.Info("Opening settings view.");
            ViewHost.Content = new SettingsView(NavigateHome, _state, _stateService, _gameInstallService);
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to open settings view.", exception);
            Close();
        }
    }

    private void OpenUe4ss()
    {
        try
        {
            AppLogger.Info("Opening UE4SS view.");
            ViewHost.Content = new Ue4ssView(NavigateHome, _state, _stateService, _gameInstallService);
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to open UE4SS view.", exception);
            Close();
        }
    }

    private void BrowseMods()
    {
        try
        {
            AppLogger.Info($"Opening mods folder: {AppPaths.ModsDirectory}.");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppPaths.ModsDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to open mods folder from home view.", exception);
        }
    }

    private void ExitApplication()
    {
        AppLogger.Info("Exit requested from home view.");
        Close();
    }
}
