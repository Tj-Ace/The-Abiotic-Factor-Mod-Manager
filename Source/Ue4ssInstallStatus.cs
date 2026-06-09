namespace AbioticLoader;

public sealed class Ue4ssInstallStatus
{
    public string BundlePath { get; init; } = string.Empty;

    public string BundleLabel { get; init; } = string.Empty;

    public string? InstallRoot { get; init; }

    public bool InstallDetected { get; init; }

    public bool Installed { get; init; }

    public bool UpToDate { get; init; }

    public bool InDirectory { get; init; }

    public bool VersionFetched { get; init; }

    public int BundleFileCount { get; init; }

    public int InstalledFileCount { get; init; }

    public string BundleBuildText { get; init; } = string.Empty;

    public string? LatestReleaseVersionText { get; init; }

    public string? LatestReleaseBuildText { get; init; }

    public string VersionText { get; init; } = string.Empty;

    public string StatusText { get; init; } = string.Empty;

    public string SummaryText { get; init; } = string.Empty;
}
