using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AbioticLoader;

public partial class HomeView : UserControl
{
    private readonly Action _launchGame;
    private readonly Action _openUe4ss;
    private readonly Action _openMods;
    private readonly Action _openSettings;
    private readonly Action _openCredits;
    private readonly Action _browseMods;
    private readonly Action _exitApplication;
    private readonly AppState _state;
    private readonly ModLibraryService _modLibraryService;
    private readonly DispatcherTimer _clockTimer = new();

    public HomeView(Action launchGame, Action openUe4ss, Action openMods, Action openSettings, Action openCredits, Action browseMods, Action exitApplication, AppState state, ModLibraryService modLibraryService)
    {
        InitializeComponent();

        _launchGame = launchGame;
        _openUe4ss = openUe4ss;
        _openMods = openMods;
        _openSettings = openSettings;
        _openCredits = openCredits;
        _browseMods = browseMods;
        _exitApplication = exitApplication;
        _state = state;
        _modLibraryService = modLibraryService;

        LoadBrandImages();
        BuildFolderTiles();
        DeepFieldButton.Click += (_, _) => OpenCredits();
        ExitButton.Click += (_, _) => _exitApplication();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (_, _) => UpdateClock();
        Loaded += (_, _) => _clockTimer.Start();
        Unloaded += (_, _) => _clockTimer.Stop();
        RefreshStatus();
        UpdateClock();
        AppLogger.Info("Home view initialized.");
    }

    private void LoadBrandImages()
    {
        LoadImage(BrandImage, Path.Combine(AppPaths.AssetsDirectory, "Gate Logo Pink.png"), "brand logo");
        LoadImage(DeepFieldImage, Path.Combine(AppPaths.AssetsDirectory, "Deep Field White.png"), "deep field logo");
    }

    private void LoadImage(Image target, string path, string label)
    {
        if (File.Exists(path))
        {
            target.Source = new BitmapImage(new Uri(path, UriKind.Absolute));
            AppLogger.Info($"Loaded {label} from {path}.");
        }
        else
        {
            AppLogger.Warn($"{label} missing at {path}.");
        }
    }

    private void BuildFolderTiles()
    {
        FolderHost.Children.Add(CreateTile(
            "Start Cascade Facility Simultaion",
            "play.png",
            _launchGame,
            @"GATE-Main-Server\Abe\Cascade-Simulaton-Project\Start.bat"));

        FolderHost.Children.Add(CreateTile(
            "UE4SS_Manager.exe",
            "terminal.png",
            _openUe4ss,
            @"GATE-Main-Server\UE4SS_Manager.exe"));

        FolderHost.Children.Add(CreateTile(
            "Mod_Manager.exe",
            "terminal.png",
            _openMods,
            @"GATE-Main-Server\Mod_Manager.exe"));

        FolderHost.Children.Add(CreateTile(
            "Settings.exe",
            "settings.png",
            _openSettings,
            @"GATE-Main-Server\Settings.exe"));

    }

    private void OpenCredits()
    {
        AppLogger.Info("Deep Field logo clicked.");
        _openCredits();
    }

    private Button CreateTile(string title, string iconFileName, Action clickAction, string? pathLabel = null)
    {
        var iconPath = Path.Combine(AppPaths.AssetsDirectory, iconFileName);

        var icon = new Image
        {
            Width = 52,
            Height = 52,
            Margin = new Thickness(0, 0, 0, 8),
            Stretch = Stretch.Uniform
        };

        if (File.Exists(iconPath))
        {
            icon.Source = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
        }

        var stack = new StackPanel
        {
            Margin = new Thickness(6),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top
        };

        stack.Children.Add(icon);
        stack.Children.Add(new TextBlock
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(17, 56, 61)),
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Width = 250,
            Effect = new DropShadowEffect
            {
                BlurRadius = 0,
                ShadowDepth = 1,
                Color = Colors.White,
                Opacity = 0.85
            }
        });

        if (!string.IsNullOrWhiteSpace(pathLabel))
        {
            stack.Children.Add(new TextBlock
            {
                Text = pathLabel,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(28, 59, 63)),
                FontSize = 9,
                Margin = new Thickness(0, 6, 0, 0),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Width = 250
            });
        }

        var button = new Button
        {
            Height = 178,
            Margin = new Thickness(8, 0, 8, 18),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Content = stack,
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        button.Click += (_, _) =>
        {
            AppLogger.Info($"{title} tile clicked.");
            clickAction();
        };

        button.MouseEnter += (_, _) => button.Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255));
        button.MouseLeave += (_, _) => button.Background = Brushes.Transparent;
        return button;
    }

    private void RefreshStatus()
    {
        StatusText.Text = "Current User: astern@cascade.internal.gate";
    }

    private void UpdateClock()
    {
        ClockText.Text = $"{DateTime.Now.ToString("h:mm tt", CultureInfo.InvariantCulture).ToUpperInvariant()} {DateTime.Now.Day:00}/{DateTime.Now.Month:00}/1993";
    }
}
