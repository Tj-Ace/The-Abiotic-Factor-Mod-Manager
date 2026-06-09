using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AbioticLoader;

public partial class ModsView : UserControl
{
    private readonly Action _backHome;
    private readonly AppState _state;
    private readonly ModLibraryService _modLibraryService;
    private readonly AppStateService _stateService;
    private readonly GameInstallService _gameInstallService = new();
    private readonly ObservableCollection<ModEntry> _entries = [];
    private ICollectionView? _modView;
    private readonly DispatcherTimer _feedbackTimer = new();
    private Button? _applyButton;
    private Button? _toggleButton;
    private Button? _addModsButton;
    private Button? _modsFolderButton;
    private Button? _ue4ssFolderButton;
    private Button? _paksFolderButton;
    private string _searchText = string.Empty;

    public ModsView(Action backHome, AppState state, ModLibraryService modLibraryService, AppStateService stateService)
    {
        InitializeComponent();

        _backHome = backHome;
        _state = state;
        _modLibraryService = modLibraryService;
        _stateService = stateService;

        BackButton.Click += (_, _) =>
        {
            AppLogger.Info("Back button clicked from mods view.");
            _backHome();
        };

        CloseButton.Click += (_, _) =>
        {
            AppLogger.Info("Close button clicked from mods view.");
            Application.Current.Shutdown();
        };

        BackButton.Content = CreateIconImage("back.png");
        CloseButton.Content = CreateIconImage("close.png");

        SearchBox.Text = string.Empty;
        SearchBox.TextChanged += (_, _) =>
        {
            _searchText = SearchBox.Text.Trim();
            _modView?.Refresh();
        };

        ModList.SelectionChanged += (_, _) => RefreshSelectionPanel();
        ModList.MouseDoubleClick += (_, _) => ToggleSelectedMod();
        ModList.AddHandler(Button.ClickEvent, new RoutedEventHandler(EntryFolderButton_Click));

        _feedbackTimer.Interval = TimeSpan.FromSeconds(2.2);
        _feedbackTimer.Tick += (_, _) =>
        {
            _feedbackTimer.Stop();
            FeedbackPopup.Visibility = Visibility.Collapsed;
        };

        EnsureInstallPath();
        ConfigureView();
        AppLogger.Info("Mods view initialized.");
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
            InstallStatusText.Text = "INSTALL DETECTED";
            return;
        }

        var detectedPath = _gameInstallService.DetectInstallPath();
        if (string.IsNullOrWhiteSpace(detectedPath))
        {
            InstallStatusText.Text = "INSTALL NOT FOUND";
            return;
        }

        _state.GameInstallPath = detectedPath;
        _stateService.Save(_state);
        InstallStatusText.Text = "INSTALL DETECTED";
        AppLogger.Info($"Game install path saved as {_state.GameInstallPath}.");
    }

    private void ConfigureView()
    {
        PathRootText.Text = @"GATE-Main-Server\Mod-Loader.exe";
        PathLeafText.Text = string.Empty;
        InstallStatusText.Text = string.Empty;

        LoadMods();
        BuildActions();

        _modView = CollectionViewSource.GetDefaultView(_entries);
        _modView.Filter = FilterModEntry;
        ModList.ItemsSource = _modView;
        _modView.Refresh();
        ModList.SelectedItem = null;
        RefreshSelectionPanel();
    }

    private bool FilterModEntry(object item)
    {
        if (item is not ModEntry entry)
        {
            return false;
        }

        if (entry.Id == "placeholder:no-mods")
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(_searchText))
        {
            return true;
        }

        return entry.DisplayName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
               entry.Kind.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
               entry.StatusLabel.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
               entry.SourcePath.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
               entry.Summary.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void LoadMods()
    {
        _entries.Clear();

        foreach (var entry in _modLibraryService.Scan(_state))
        {
            _entries.Add(entry);
        }

        AppLogger.Info($"Loaded {_entries.Count} entry(s) into the mods list.");
    }

    private void BuildActions()
    {
        ActionRow.Children.Clear();
        _applyButton = CreateActionButton("APPLY", (_, _) => ApplyOrRemoveSelectedMod());
        _toggleButton = CreateActionButton("Enable", (_, _) => ToggleSelectedMod());
        _addModsButton = CreateActionButton("Add Mods", (_, _) => OpenModsDirectory());
        _modsFolderButton = CreateActionButton("Mods Folder", (_, _) => OpenSelectedFolder());
        _ue4ssFolderButton = CreateActionButton("UE4SS Folder", (_, _) => OpenBundledUe4ssFolder());
        _paksFolderButton = CreateActionButton("PAKS Folder", (_, _) => OpenGamePaksFolder());

        ActionRow.Children.Add(_applyButton);
        ActionRow.Children.Add(_toggleButton);
        ActionRow.Children.Add(_addModsButton);
        ActionRow.Children.Add(_modsFolderButton);
        ActionRow.Children.Add(_ue4ssFolderButton);
        ActionRow.Children.Add(_paksFolderButton);
    }

    private Button CreateActionButton(string title, RoutedEventHandler click)
    {
        var button = new Button
        {
            Content = title,
            Margin = new Thickness(6),
            Padding = new Thickness(14, 10, 14, 10),
            Height = 52,
            Background = new SolidColorBrush(Color.FromRgb(217, 244, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(111, 175, 194)),
            Foreground = new SolidColorBrush(Color.FromRgb(17, 17, 17)),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Tahoma"),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        button.Click += click;
        return button;
    }

    private void RefreshSelectionPanel()
    {
        var selected = ModList.SelectedItem as ModEntry;
        if (selected is null)
        {
            DetailTitle.Text = string.Empty;
            DetailMeta.Text = string.Empty;
            DetailPath.Text = string.Empty;
            DetailSummary.Text = string.Empty;
            SelectedPathText.Text = string.Empty;
            CopiedBadge.Text = string.Empty;
            StateBadge.Text = string.Empty;
            KindBadge.Text = string.Empty;
            AppliedPathText.Text = string.Empty;
            PackageKindText.Text = string.Empty;
            UpdateActionButtons(null);
            UpdateToggleButton(null);
            return;
        }

        _state.LastSelectedModId = selected.Id;
        AppLogger.Info($"Selected mod entry: {selected.DisplayName} ({selected.Id}).");

        var installPath = _state.GameInstallPath;
        var targetPath = string.IsNullOrWhiteSpace(installPath)
            ? string.Empty
            : _gameInstallService.GetDeploymentTargetPath(selected, installPath);

        DetailTitle.Text = StripVersionSuffix(selected.DisplayName);
        DetailMeta.Text = $"Status: {(selected.IsCopiedToGameFolder ? (selected.IsEnabled ? "Applied" : "Disabled") : "Detected")}";
        DetailPath.Text = Path.GetFileName(selected.SourcePath);
        SelectedPathText.Text = selected.SourcePath;
        DetailSummary.Text = selected.Summary;

        CopiedBadge.Text = selected.IsCopiedToGameFolder ? "COPIED TO GAME" : "DETECTED";
        StateBadge.Text = selected.IsEnabled ? "STAGED ENABLED" : "STAGED DISABLED";
        KindBadge.Text = selected.IsBundled ? "UE4SS" : selected.Kind;
        var gameRoot = string.IsNullOrWhiteSpace(installPath) ? string.Empty : _gameInstallService.GetGameRootPath(installPath);
        var relativeTarget = string.IsNullOrWhiteSpace(gameRoot) || string.IsNullOrWhiteSpace(targetPath)
            ? string.Empty
            : targetPath.Contains(" | ", StringComparison.Ordinal)
                ? targetPath
                : Path.GetRelativePath(gameRoot, targetPath);
        AppliedPathText.Text = string.IsNullOrWhiteSpace(gameRoot) ? string.Empty : $"ROOT: {gameRoot}";
        PackageKindText.Text = string.IsNullOrWhiteSpace(relativeTarget)
            ? string.Empty
            : $"MOD PATH: {relativeTarget} [{_gameInstallService.GetLogicalDeploymentDisplayPath(selected, selected.IsEnabled)}]";
        UpdateActionButtons(selected);
        UpdateToggleButton(selected);
        ShowFeedback($"Selected {selected.DisplayName}.");
    }

    private void SetSelectedModState(bool enabled)
    {
        var selected = ModList.SelectedItem as ModEntry;
        if (selected is null)
        {
            AppLogger.Warn("State change requested with no selection.");
            ShowFeedback("Select a mod first.");
            return;
        }

        if (!selected.CanToggle)
        {
            AppLogger.Info($"State change ignored for bundled entry {selected.DisplayName}.");
            ShowFeedback("That mod cannot be toggled.");
            return;
        }

        if (!selected.IsCopiedToGameFolder)
        {
            AppLogger.Info($"State change ignored because {selected.DisplayName} is not applied.");
            ShowFeedback("Apply the mod first.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_state.GameInstallPath) || !Directory.Exists(_state.GameInstallPath))
        {
            ShowFeedback("No game install detected.");
            return;
        }

        if (_gameInstallService.SetDeploymentEnabled(selected, _state.GameInstallPath, enabled))
        {
            _modLibraryService.SaveState(_state, _entries);
            _stateService.Save(_state);
            _modView?.Refresh();
            RefreshSelectionPanel();
            AppLogger.Info($"{(enabled ? "Enabled" : "Disabled")} deployed mod {selected.DisplayName}.");
            ShowFeedback($"{selected.DisplayName}: {(enabled ? "enabled" : "disabled")}.");
        }
    }

    private void ToggleSelectedMod()
    {
        var selected = ModList.SelectedItem as ModEntry;
        if (selected is null)
        {
            AppLogger.Warn("Toggle requested with no selection.");
            return;
        }

        if (!selected.CanToggle)
        {
            AppLogger.Info($"Toggle ignored for bundled entry {selected.DisplayName}.");
            return;
        }

        SetSelectedModState(!selected.IsEnabled);
    }

    private void ApplyOrRemoveSelectedMod()
    {
        var selected = ModList.SelectedItem as ModEntry;
        if (selected is null)
        {
            ShowFeedback("Select a mod first.");
            return;
        }

        EnsureInstallPath();

        if (string.IsNullOrWhiteSpace(_state.GameInstallPath) || !Directory.Exists(_state.GameInstallPath))
        {
            ShowFeedback("No game install detected.");
            return;
        }

        try
        {
            _gameInstallService.SetDeploymentState(selected, _state.GameInstallPath, !selected.IsCopiedToGameFolder);
            _modLibraryService.SaveState(_state, _entries);
            _stateService.Save(_state);
            _modView?.Refresh();
            RefreshSelectionPanel();
            ShowFeedback(selected.IsCopiedToGameFolder ? "Mod applied." : "Mod removed.");
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed while toggling a single mod deployment.", exception);
            ShowFeedback("Action failed.");
        }
    }

    private void ReloadView()
    {
        AppLogger.Info("Rescanning mods folder.");
        EnsureInstallPath();
        ConfigureView();
        ShowFeedback("Mods list refreshed.");
    }

    private void ApplyMods()
    {
        try
        {
            EnsureInstallPath();

            if (string.IsNullOrWhiteSpace(_state.GameInstallPath) || !Directory.Exists(_state.GameInstallPath))
            {
                AppLogger.Warn("Apply requested but no Abiotic Factor install was detected.");
                DetailSummary.Text = "Abiotic Factor install not detected. Please install the game or set the path before applying mods.";
                return;
            }

            _gameInstallService.ApplyDeployments(_entries, _state.GameInstallPath);
            _modLibraryService.SaveState(_state, _entries);
            _stateService.Save(_state);
            ReloadView();
            AppLogger.Info("Apply Mods clicked; staged mod state copied into the game folder.");
            ShowFeedback("Mods applied to the game folder.");
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed while applying mods.", exception);
            ShowFeedback("Apply failed.");
        }
    }

    private static string StripVersionSuffix(string value)
    {
        var trimmed = value.Trim();
        trimmed = System.Text.RegularExpressions.Regex.Replace(
            trimmed,
            @"(?i)(?:[\s._-]*v?\d+(?:\.\d+){1,4})+$",
            string.Empty);

        trimmed = trimmed.Trim(' ', '.', '-', '_');
        return string.IsNullOrWhiteSpace(trimmed) ? value : trimmed;
    }

    private void OpenSelectedFolder()
    {
        OpenFolderPath(AppPaths.ModsDirectory, "Mods folder");
    }

    private void OpenModsDirectory()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.ModsDirectory);

            var dialog = new OpenFileDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Filter = "Mod archives (*.zip;*.7z;*.rar)|*.zip;*.7z;*.rar|Zip archives (*.zip)|*.zip|7z archives (*.7z)|*.7z|RAR archives (*.rar)|*.rar|All files (*.*)|*.*",
                Multiselect = true,
                Title = "Select mod archives to add"
            };

            if (dialog.ShowDialog() != true)
            {
                ShowFeedback("No files selected.");
                return;
            }

            var copiedCount = 0;
            foreach (var sourceFile in dialog.FileNames)
            {
                var targetFile = Path.Combine(AppPaths.ModsDirectory, Path.GetFileName(sourceFile));
                File.Copy(sourceFile, targetFile, true);
                copiedCount++;
                AppLogger.Info($"Imported mod archive: {sourceFile} -> {targetFile}");
            }

            ReloadView();
            ShowFeedback(copiedCount == 1 ? "1 archive added to Mods." : $"{copiedCount} archives added to Mods.");
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to open mods directory.", exception);
            ShowFeedback("Import failed.");
        }
    }

    private void OpenBundledUe4ssFolder()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_state.GameInstallPath) || !Directory.Exists(_state.GameInstallPath))
            {
                ShowFeedback("No game install detected.");
                return;
            }

            var gameRoot = _gameInstallService.GetGameRootPath(_state.GameInstallPath);
            var ue4ssPath = Path.Combine(gameRoot, "Binaries", "Win64", "ue4ss", "Mods");
            if (!Directory.Exists(ue4ssPath))
            {
                ShowFeedback("UE4SS is not installed.");
                return;
            }

            OpenFolderPath(ue4ssPath, "UE4SS folder");
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to open bundled UE4SS folder.", exception);
            ShowFeedback("Could not open UE4SS folder.");
        }
    }

    private void OpenGamePaksFolder()
    {
        if (string.IsNullOrWhiteSpace(_state.GameInstallPath) || !Directory.Exists(_state.GameInstallPath))
        {
            AppLogger.Warn("Open PAKS folder requested but the install path is unavailable.");
            ShowFeedback("No game install detected.");
            return;
        }

        try
        {
            var paksPath = Path.Combine(_gameInstallService.GetGameRootPath(_state.GameInstallPath), "Content", "Paks");
            OpenFolderPath(paksPath, "PAKS folder");
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to open PAKS folder.", exception);
            ShowFeedback("Could not open PAKS folder.");
        }
    }

    private void EntryFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement element || element.DataContext is not ModEntry entry)
        {
            return;
        }

        OpenFolderForEntry(entry);
        e.Handled = true;
    }

    private void OpenFolderForEntry(ModEntry entry)
    {
        try
        {
            var path = Directory.Exists(entry.SourcePath)
                ? entry.SourcePath
                : Path.GetDirectoryName(entry.SourcePath) ?? AppPaths.ModsDirectory;
            OpenFolderPath(path, entry.DisplayName);
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to open mod folder.", exception);
            ShowFeedback("Could not open mod folder.");
        }
    }

    private void OpenFolderPath(string path, string label)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });

        AppLogger.Info($"Opened {label}: {path}.");
        ShowFeedback($"Opened {label}.");
    }

    private void UpdateToggleButton(ModEntry? selected)
    {
        if (_toggleButton is null)
        {
            return;
        }

        if (selected is null)
        {
            _toggleButton.Content = "Enable";
            _toggleButton.IsEnabled = false;
            _toggleButton.Opacity = 0.45;
            return;
        }

        _toggleButton.Content = selected.IsEnabled ? "Disable" : "Enable";
        _toggleButton.IsEnabled = selected.CanToggle && selected.IsCopiedToGameFolder;
        _toggleButton.Opacity = _toggleButton.IsEnabled ? 1.0 : 0.45;
    }

    private void UpdateActionButtons(ModEntry? selected)
    {
        if (_applyButton is null || _addModsButton is null || _modsFolderButton is null || _ue4ssFolderButton is null || _paksFolderButton is null)
        {
            return;
        }

        var gameInstallPath = _state.GameInstallPath;
        var hasSelection = selected is not null;
        var hasInstall = !string.IsNullOrWhiteSpace(gameInstallPath) && Directory.Exists(gameInstallPath);
        var ue4ssInstalled = hasInstall && Directory.Exists(Path.Combine(_gameInstallService.GetGameRootPath(gameInstallPath!), "Binaries", "Win64", "ue4ss", "Mods"));

        _applyButton.Content = hasSelection && selected!.IsCopiedToGameFolder ? "REMOVE" : "APPLY";
        _applyButton.IsEnabled = hasSelection && hasInstall;
        _applyButton.Background = hasSelection && selected!.IsCopiedToGameFolder
            ? new SolidColorBrush(Color.FromRgb(255, 228, 228))
            : new SolidColorBrush(Color.FromRgb(229, 229, 229));
        _applyButton.Foreground = new SolidColorBrush(Color.FromRgb(17, 17, 17));

        _addModsButton.IsEnabled = true;
        _modsFolderButton.IsEnabled = true;
        _ue4ssFolderButton.IsEnabled = ue4ssInstalled;
        _paksFolderButton.IsEnabled = hasInstall;
        _applyButton.Opacity = _applyButton.IsEnabled ? 1.0 : 0.45;
        _ue4ssFolderButton.Opacity = _ue4ssFolderButton.IsEnabled ? 1.0 : 0.45;
    }

    private void ShowFeedback(string message)
    {
        FeedbackText.Text = message;
        FeedbackPopup.Visibility = Visibility.Visible;
        _feedbackTimer.Stop();
        _feedbackTimer.Start();
    }

}
