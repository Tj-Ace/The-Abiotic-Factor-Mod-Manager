using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AbioticLoader;

public partial class SettingsView : UserControl
{
    private const string CurrentModLoaderVersion = "0.3";
    private readonly Action _backHome;
    private readonly AppState _state;
    private readonly AppStateService _stateService;
    private readonly GameInstallService _gameInstallService;

    public SettingsView(Action backHome, AppState state, AppStateService stateService, GameInstallService gameInstallService)
    {
        InitializeComponent();

        _backHome = backHome;
        _state = state;
        _stateService = stateService;
        _gameInstallService = gameInstallService;

        BackButton.Click += (_, _) => _backHome();
        CloseButton.Click += (_, _) => Application.Current.Shutdown();

        BackButton.Content = CreateIconImage("back.png");
        CloseButton.Content = CreateIconImage("close.png");

        PathRootText.Text = @"GATE-Main-Server\Settings.exe";
        PathLeafText.Text = string.Empty;
        InstallStatusText.Text = string.Empty;

        EnsureInstallPath();
        BuildActions();
        AutoCheckBox.IsChecked = _state.AutoCheckModLoaderUpdates;
        RefreshInfo();

        AppLogger.Info("Settings view initialized.");
    }

    private void EnsureInstallPath()
    {
        if (!string.IsNullOrWhiteSpace(_state.GameInstallPath) && Directory.Exists(_state.GameInstallPath))
        {
            return;
        }

        var detectedPath = _gameInstallService.DetectInstallPath();
        if (string.IsNullOrWhiteSpace(detectedPath))
        {
            return;
        }

        _state.GameInstallPath = detectedPath;
        _stateService.Save(_state);
    }

    private void BuildActions()
    {
        ActionGrid.Children.Clear();

        ActionGrid.Children.Add(CreateActionButton("Open Logs", () => OpenFolder(AppPaths.LogsDirectory)));
        ActionGrid.Children.Add(CreateActionButton("Set Install Path", PickInstallPath));
        ActionGrid.Children.Add(CreateActionButton("Open Game Folder", OpenGameFolder));
        ActionGrid.Children.Add(CreateActionButton("Reset All Warnings", ResetAllWarnings));
        ActionGrid.Children.Add(CreateActionButton("Check Mod Loader Updates", CheckModLoaderUpdates));
        ActionGrid.Children.Add(CreateActionButton("Upcoming Features", ShowUpcomingFeatures));
        ActionGrid.Children.Add(CreateComingSoonButton("Dark Mode"));
        ActionGrid.Children.Add(CreateComingSoonButton("Main Menu\nCustomizer"));
    }

    private Button CreateActionButton(string label, Action action)
    {
        var button = new Button
        {
            Margin = new Thickness(8),
            Height = 130,
            Background = new SolidColorBrush(Color.FromRgb(219, 248, 247)),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(3),
            Cursor = System.Windows.Input.Cursors.Hand,
            Content = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Color.FromRgb(16, 18, 20)),
                FontSize = 24,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            }
        };

        button.Click += (_, _) => action();
        button.MouseEnter += (_, _) => button.Background = new SolidColorBrush(Color.FromRgb(237, 255, 255));
        button.MouseLeave += (_, _) => button.Background = new SolidColorBrush(Color.FromRgb(219, 248, 247));
        return button;
    }

    private static Button CreateComingSoonButton(string label)
    {
        return new Button
        {
            Margin = new Thickness(8),
            Height = 130,
            Background = new SolidColorBrush(Color.FromRgb(222, 222, 222)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(154, 154, 154)),
            BorderThickness = new Thickness(3),
            IsEnabled = false,
            Opacity = 0.72,
            Content = new TextBlock
            {
                Text = $"{label}\nCOMING SOON",
                Foreground = new SolidColorBrush(Color.FromRgb(78, 78, 78)),
                FontSize = 20,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private void RefreshInfo()
    {
        InstallPathText.Text = string.IsNullOrWhiteSpace(_state.GameInstallPath)
            ? "INSTALL PATH: NOT FOUND"
            : $"INSTALL PATH: {_state.GameInstallPath}";
        VersionText.Text = $"CURRENT VERSION: {CurrentModLoaderVersion}";
    }

    private void AutoCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _state.AutoCheckModLoaderUpdates = AutoCheckBox.IsChecked == true;
        _stateService.Save(_state);
        AppLogger.Info($"Auto-check for mod loader updates set to {_state.AutoCheckModLoaderUpdates}.");
    }

    private void OpenFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            MessageBox.Show($"Folder not found:\n{path}", "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private void OpenGameFolder()
    {
        EnsureInstallPath();

        if (string.IsNullOrWhiteSpace(_state.GameInstallPath) || !Directory.Exists(_state.GameInstallPath))
        {
            MessageBox.Show("No game install folder was detected.", "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        OpenFolder(_state.GameInstallPath);
    }

    private void PickInstallPath()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Abiotic Factor executable",
            Filter = "AbioticFactor-Win64-Shipping.exe|AbioticFactor-Win64-Shipping.exe",
            CheckFileExists = true,
            CheckPathExists = true,
            RestoreDirectory = true
        };

        if (!string.IsNullOrWhiteSpace(_state.GameInstallPath) && Directory.Exists(_state.GameInstallPath))
        {
            dialog.InitialDirectory = _state.GameInstallPath;
        }

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var chosenExe = dialog.FileName;
        var gameRoot = _gameInstallService.GetGameRootPath(chosenExe);
        _state.GameInstallPath = gameRoot;
        _stateService.Save(_state);
        RefreshInfo();

        MessageBox.Show($"Install path set to:\n{gameRoot}", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        AppLogger.Info($"Game install path set by user to {gameRoot}.");
    }

    private void ResetAllWarnings()
    {
        _state.SawUe4ssWarning = false;
        _state.SawMissingInstallWarning = false;
        _state.LastNotifiedModLoaderVersion = null;
        _stateService.Save(_state);
        MessageBox.Show("All warnings have been reset.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CheckModLoaderUpdates()
    {
        try
        {
            var currentVersion = new Version(0, 3);
            var latestVersion = _gameInstallService.GetLatestModLoaderVersion();

            if (latestVersion is null)
            {
                MessageBox.Show(
                    "Could not find a published GitHub version for the mod loader.\n\nCurrent version: 0.3",
                    "Mod Loader Updates",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                AppLogger.Warn("Mod loader update check could not determine a latest version.");
                return;
            }

            var message = latestVersion > currentVersion
                ? $"A newer mod loader version is available.\n\nCurrent: {CurrentModLoaderVersion}\nLatest: {latestVersion}"
                : $"No newer version was found.\n\nCurrent: {CurrentModLoaderVersion}\nLatest: {latestVersion}";

            MessageBox.Show(message, "Mod Loader Updates", MessageBoxButton.OK, MessageBoxImage.Information);
            AppLogger.Info($"Mod loader update check complete. Current {CurrentModLoaderVersion}, latest {latestVersion}.");
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to check mod loader updates.", exception);
            MessageBox.Show(
                $"Update check failed.\n\nCurrent version: {CurrentModLoaderVersion}",
                "Mod Loader Updates",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ShowUpcomingFeatures()
    {
        MessageBox.Show(
            "Upcoming Features:\n\n- Dark Mode\n- Mod List Sorting\n- Ui Redesign\n- Main Menu Customizer\n- And Much More",
            "Upcoming Features",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static Image CreateIconImage(string fileName)
    {
        return new Image
        {
            Source = LoadAssetImage(fileName),
            Width = 18,
            Height = 18,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static ImageSource? LoadAssetImage(string fileName)
    {
        var path = Path.Combine(AppPaths.AssetsDirectory, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
