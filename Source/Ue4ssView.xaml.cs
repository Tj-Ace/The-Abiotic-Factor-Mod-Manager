using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AbioticLoader;

public partial class Ue4ssView : UserControl
{
    private readonly Action _backHome;
    private readonly AppState _state;
    private readonly AppStateService _stateService;
    private readonly GameInstallService _gameInstallService;
    private readonly DispatcherTimer _popupTimer = new();

    public Ue4ssView(Action backHome, AppState state, AppStateService stateService, GameInstallService gameInstallService)
    {
        InitializeComponent();

        _backHome = backHome;
        _state = state;
        _stateService = stateService;
        _gameInstallService = gameInstallService;

        BackButton.Click += (_, _) => _backHome();
        CloseButton.Click += (_, _) => Application.Current.Shutdown();
        PatchGameButton.Click += (_, _) => PatchGame();
        OpenDirectoryButton.Click += (_, _) => OpenDirectory();
        CheckUpdatesButton.Click += (_, _) => CheckUpdates();
        OpenModPageButton.Click += (_, _) => OpenModPage();
        RevertInstallButton.Click += (_, _) => RevertInstall();
        UpdateArchiveButton.Click += (_, _) => UpdateArchive();

        BackButton.Content = CreateIconImage("back.png");
        CloseButton.Content = CreateIconImage("close.png");
        Ue4ssLogoImage.Source = LoadAssetImage("UE4SS.png");

        _popupTimer.Interval = TimeSpan.FromSeconds(2.8);
        _popupTimer.Tick += (_, _) =>
        {
            _popupTimer.Stop();
            FeedbackPopup.Visibility = Visibility.Collapsed;
        };

        RefreshView();
        AppLogger.Info("UE4SS view initialized.");
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
        AppLogger.Info($"Game install path saved as {_state.GameInstallPath} for UE4SS management.");
    }

    private Ue4ssInstallStatus RefreshView()
    {
        EnsureInstallPath();

        var status = _gameInstallService.InspectUe4ss(_state.GameInstallPath, _state.Ue4ssSelectedUpdatePath);
        ApplyStatus(status);
        return status;
    }

    private void ApplyStatus(Ue4ssInstallStatus status)
    {
        PathRootText.Text = "GATE-Main-Server";
        PathLeafText.Text = "UE4SS_Manager.exe";
        VersionText.Text = status.VersionText;

        SetStateBox(AppliedBox, status.Installed);
        SetStateBox(UpToDateBox, status.UpToDate);
        SetStateBox(InDirectoryBox, status.InDirectory);

        TerminalBundleBuildText.Text = $"LOCAL BUNDLE BUILD: {status.BundleBuildText}";
        TerminalLatestBuildText.Text = status.LatestReleaseBuildText is not null
            ? $"LATEST RELEASE BUILD: {status.LatestReleaseBuildText}"
            : status.LatestReleaseVersionText is not null
                ? $"LATEST RELEASE VERSION: V{status.LatestReleaseVersionText}"
                : "LATEST RELEASE: CHECK UNAVAILABLE";
        TerminalStatusText.Text = $"STATUS: {status.StatusText}";
        TerminalSummaryText.Text = status.SummaryText;

        AppLogger.Info($"UE4SS refreshed: {status.StatusText}; version {VersionText.Text}; local bundle build {status.BundleBuildText}.");
    }

    private static void SetStateBox(Border box, bool active)
    {
        box.Background = new SolidColorBrush(active ? Color.FromRgb(218, 243, 255) : Color.FromRgb(255, 255, 255));
        box.BorderBrush = new SolidColorBrush(active ? Color.FromRgb(42, 126, 130) : Color.FromRgb(140, 140, 140));
        box.Child = new Image
        {
            Source = LoadAssetImage(active ? "check.png" : "close.png"),
            Width = 22,
            Height = 22,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private void OpenModPage()
    {
        try
        {
            ShowFeedback("GATE CHAT", "OPENING GITHUB RELEASE PAGE");
            Process.Start(new ProcessStartInfo
            {
                FileName = GameInstallService.Ue4ssGithubReleaseUrl,
                UseShellExecute = true
            });

            AppLogger.Info($"Opened UE4SS mod page: {GameInstallService.Ue4ssGithubReleaseUrl}.");
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to open UE4SS mod page.", exception);
        }
    }

    private void CheckUpdates()
    {
        try
        {
            ShowFeedback("GATE CHAT", "CHECKING GITHUB RELEASE...");
            var status = RefreshView();
            var latestVersion = status.LatestReleaseBuildText is not null
                ? status.LatestReleaseBuildText
                : status.LatestReleaseVersionText is null
                    ? "UNAVAILABLE"
                    : $"V{status.LatestReleaseVersionText}";
            var message = string.Join(Environment.NewLine,
                $"LOCAL BUNDLE BUILD: {status.BundleBuildText}",
                $"LATEST RELEASE: {latestVersion}",
                $"RESULT: {status.StatusText}");
            ShowFeedback("UE4SS UPDATE INFO", message);
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed while checking UE4SS updates.", exception);
        }
    }

    private void PatchGame()
    {
        try
        {
            ShowFeedback("GATE CHAT", "PATCHING UE4SS...");
            EnsureInstallPath();

            if (string.IsNullOrWhiteSpace(_state.GameInstallPath) || !Directory.Exists(_state.GameInstallPath))
            {
                AppLogger.Warn("Patch requested but no Abiotic Factor install was detected.");
                RefreshView();
                return;
            }

            var applied = _gameInstallService.ApplyBundledUe4ss(_state.GameInstallPath);
            AppLogger.Info(applied
                ? "Bundled UE4SS patched into the game install."
                : "Bundled UE4SS patch failed.");
            ShowFeedback("PATCH STATUS", applied ? "UE4SS patched into game." : "Patch failed.");
            RefreshView();
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed while patching UE4SS to the game.", exception);
        }
    }

    private void OpenDirectory()
    {
        try
        {
            ShowFeedback("GATE CHAT", "OPENING DIRECTORY");
            EnsureInstallPath();

            var directory = string.IsNullOrWhiteSpace(_state.GameInstallPath)
                ? AppPaths.ApplicationRoot
                : Path.Combine(_gameInstallService.GetGameRootPath(_state.GameInstallPath), "Binaries", "Win64");

            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });

            AppLogger.Info($"Opened UE4SS directory: {directory}.");
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to open UE4SS directory.", exception);
        }
    }

    private void RevertInstall()
    {
        try
        {
            ShowFeedback("GATE CHAT", "REVERTING INSTALL...");
            EnsureInstallPath();

            if (string.IsNullOrWhiteSpace(_state.GameInstallPath) || !Directory.Exists(_state.GameInstallPath))
            {
                AppLogger.Warn("Revert install requested but no Abiotic Factor install was detected.");
                RefreshView();
                return;
            }

            var reverted = _gameInstallService.RemoveUe4ssInstall(_state.GameInstallPath);
            AppLogger.Info(reverted
                ? "UE4SS install reverted from the game directory."
                : "UE4SS install revert did not remove anything.");
            ShowFeedback("REVERT STATUS", reverted ? "UE4SS removed from game." : "Nothing to remove.");
            RefreshView();
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed while reverting the UE4SS install.", exception);
        }
    }

    private void UpdateArchive()
    {
        try
        {
            ShowFeedback("GATE CHAT", "UPDATING UE4SS ZIP...");
            var dialog = new OpenFileDialog
            {
                Title = "Select UE4SS zip",
                Filter = "UE4SS zip (*.zip)|*.zip|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                AppLogger.Info("UE4SS archive update canceled.");
                return;
            }

            Directory.CreateDirectory(AppPaths.Ue4ssDirectory);
            var destination = Path.Combine(AppPaths.Ue4ssDirectory, Path.GetFileName(dialog.FileName));
            File.Copy(dialog.FileName, destination, true);
            _state.Ue4ssSelectedUpdatePath = destination;
            _stateService.Save(_state);
            AppLogger.Info($"Updated bundled UE4SS archive from {dialog.FileName} to {destination}.");
            ShowFeedback("GATE CHAT", $"UE4SS UPDATED:\n{Path.GetFileName(dialog.FileName)}");
            RefreshView();
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed while updating the UE4SS archive.", exception);
        }
    }

    private void ShowFeedback(string title, string message)
    {
        PopupTitleText.Text = title;
        PopupMessageText.Text = message;
        FeedbackPopup.Visibility = Visibility.Visible;
        _popupTimer.Stop();
        _popupTimer.Start();
    }
}
