using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AbioticLoader;

public sealed class ModEntry : INotifyPropertyChanged
{
    private bool _isEnabled = true;
    private bool _isCopiedToGameFolder;

    public ModEntry(string id, string displayName, string sourcePath, string kind, string summary, bool isBundled)
    {
        Id = id;
        DisplayName = displayName;
        SourcePath = sourcePath;
        Kind = kind;
        Summary = summary;
        IsBundled = isBundled;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string SourcePath { get; }

    public string Kind { get; }

    public string Summary { get; }

    public bool IsBundled { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(ListLabel));
            OnPropertyChanged(nameof(ListStatusIconSource));
            OnPropertyChanged(nameof(ShowListStatusIcon));
        }
    }

    public bool IsCopiedToGameFolder
    {
        get => _isCopiedToGameFolder;
        set
        {
            if (_isCopiedToGameFolder == value)
            {
                return;
            }

            _isCopiedToGameFolder = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusIconSource));
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(ListLabel));
            OnPropertyChanged(nameof(ListStatusIconSource));
            OnPropertyChanged(nameof(ShowListStatusIcon));
        }
    }

    public bool CanToggle => !IsBundled;

    public ImageSource StatusIconSource => LoadIcon(IsCopiedToGameFolder ? "check.png" : "close.png");

    public ImageSource? ListStatusIconSource => !IsEnabled
        ? LoadIcon("close.png")
        : IsCopiedToGameFolder
            ? LoadIcon("check.png")
            : null;

    public bool ShowListStatusIcon => !IsEnabled || IsCopiedToGameFolder;

    public ImageSource FolderIconSource => LoadIcon("folder.png");

    public string StatusLabel => IsCopiedToGameFolder ? "COPIED TO GAME" : IsEnabled ? "STAGED" : "DISABLED";

    public string ListLabel => $"{(IsCopiedToGameFolder ? "✓" : "✕")} {DisplayName}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static ImageSource LoadIcon(string fileName)
    {
        var path = Path.Combine(AppPaths.AssetsDirectory, fileName);
        if (!File.Exists(path))
        {
            return default!;
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
