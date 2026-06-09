namespace AbioticLoader;

public sealed class AppState
{
    public HashSet<string> DisabledModIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? LastSelectedModId { get; set; }

    public string? GameInstallPath { get; set; }

    public int UiSequenceNumber { get; set; }

    public string? Ue4ssSelectedUpdatePath { get; set; }

    public bool SawUe4ssWarning { get; set; }

    public bool SawMissingInstallWarning { get; set; }

    public string? LastNotifiedModLoaderVersion { get; set; }

    public bool AutoCheckModLoaderUpdates { get; set; } = true;
}
