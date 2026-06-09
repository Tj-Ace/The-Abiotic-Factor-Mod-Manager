using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AbioticLoader;

public partial class CreditsView : UserControl
{
    private readonly Action _backHome;

    public CreditsView(Action backHome)
    {
        InitializeComponent();

        _backHome = backHome;

        BackButton.Click += (_, _) => _backHome();
        CloseButton.Click += (_, _) => Application.Current.Shutdown();

        BackButton.Content = CreateIconImage("back.png");
        CloseButton.Content = CreateIconImage("close.png");

        PathRootText.Text = @"GATE-Main-Server\Credits\Main.txt";
        PathLeafText.Text = string.Empty;
        InstallStatusText.Text = string.Empty;

        BuildButtons();

        AppLogger.Info("Credits view initialized.");
    }

    private void BuildButtons()
    {
        ButtonHost.Children.Clear();

        ButtonHost.Children.Add(CreateLinkTile("Nexus", "nexus.png", "https://www.nexusmods.com/abioticfactor/mods/236"));
        ButtonHost.Children.Add(CreateLinkTile("Unofficial\nMerch", "shirt.png", "https://tjstickers.us/collections/abiotic-factor-merch"));
        ButtonHost.Children.Add(CreateLinkTile("Steam", "steam.png", "https://store.steampowered.com/app/427410/Abiotic_Factor/"));
        ButtonHost.Children.Add(CreateLinkTile("Official\nMerch", "shirt.png", "https://www.abioticfactor.com/merch"));
        ButtonHost.Children.Add(CreateLinkTile("Github", "github.png", "https://github.com/Tj-Ace/The-Abiotic-Factor-Mod-Manager"));
    }

    private Button CreateLinkTile(string label, string iconFileName, string url)
    {
        var icon = new Image
        {
            Width = 74,
            Height = 74,
            Margin = new Thickness(0, 10, 0, 10),
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        icon.Source = LoadAssetImage(iconFileName);

        var labelBlock = new TextBlock
        {
            Text = label,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(16, 18, 20)),
            FontSize = 28,
            FontWeight = FontWeights.Normal,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 8, 8)
        };

        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        stack.Children.Add(icon);
        stack.Children.Add(labelBlock);

        var button = new Button
        {
            Margin = new Thickness(8, 0, 8, 0),
            Height = 220,
            Background = new SolidColorBrush(Color.FromRgb(219, 248, 247)),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(3),
            Content = stack,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        button.Click += (_, _) => Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
        button.MouseEnter += (_, _) => button.Background = new SolidColorBrush(Color.FromRgb(237, 255, 255));
        button.MouseLeave += (_, _) => button.Background = new SolidColorBrush(Color.FromRgb(219, 248, 247));
        return button;
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
