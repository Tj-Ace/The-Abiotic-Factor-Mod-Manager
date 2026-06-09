using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace AbioticLoader;

public sealed class GameInstallService
{
    public const string ShippingExecutableName = "AbioticFactor-Win64-Shipping.exe";
    public const string Ue4ssGithubReleaseUrl = "https://github.com/igromanru/AF-UE4SS/releases/tag/1.20.0";
    public const string ModLoaderGithubReleasesUrl = "https://github.com/Tj-Ace/The-Abiotic-Factor-Mod-Manager/releases";
    public const string ModLoaderGithubTagsUrl = "https://github.com/Tj-Ace/The-Abiotic-Factor-Mod-Manager/tags";

    public enum PackageType
    {
        Unknown,
        Ue4ss,
        Pak,
        Mixed
    }

    public string? DetectInstallPath()
    {
        var steamRoots = EnumerateSteamRoots().Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var steamRoot in steamRoots)
        {
            var installPath = FindInstallUnderSteamRoot(steamRoot);
            if (!string.IsNullOrWhiteSpace(installPath))
            {
                AppLogger.Info($"Detected Abiotic Factor install at {installPath}.");
                return installPath;
            }
        }

        AppLogger.Warn("Unable to auto-detect the Abiotic Factor install path.");
        return null;
    }

    public string GetGameRootPath(string installPath)
    {
        var normalized = Path.GetFullPath(installPath);
        if (Directory.Exists(Path.Combine(normalized, "Binaries", "Win64")))
        {
            return normalized;
        }

        var directory = new DirectoryInfo(normalized);
        while (directory is not null)
        {
            if (string.Equals(directory.Name, "Win64", StringComparison.OrdinalIgnoreCase) &&
                directory.Parent is { Name: "Binaries" } binariesParent &&
                binariesParent.Parent is not null)
            {
                return binariesParent.Parent.FullName;
            }

            directory = directory.Parent;
        }

        return normalized;
    }

    public string GetLaunchExecutablePath(string installPath)
    {
        var gameRoot = GetGameRootPath(installPath);
        var primary = Path.Combine(gameRoot, "Binaries", "Win64", ShippingExecutableName);
        if (File.Exists(primary))
        {
            return primary;
        }

        var fallback = Path.Combine(installPath, ShippingExecutableName);
        return File.Exists(fallback) ? fallback : primary;
    }

    public string? GetBundledUe4ssArchivePath()
    {
        if (!Directory.Exists(AppPaths.Ue4ssDirectory))
        {
            return null;
        }

        return Directory.EnumerateFiles(AppPaths.Ue4ssDirectory, "*.zip", SearchOption.TopDirectoryOnly)
            .Select(path =>
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                var versionText = TryNormalizeVersionFromFileName(fileName);
                var buildText = TryNormalizeBuildFromFileName(fileName);
                return new
                {
                    Path = path,
                    Version = TryParseVersionFromText(versionText),
                    Build = TryParseInteger(buildText),
                    Modified = File.GetLastWriteTimeUtc(path)
                };
            })
            .OrderByDescending(candidate => candidate.Version ?? new Version(0, 0, 0))
            .ThenByDescending(candidate => candidate.Build ?? -1)
            .ThenByDescending(candidate => candidate.Modified)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();
    }

    public string GetUe4ssInstallRoot(string installPath)
    {
        var gameRoot = GetGameRootPath(installPath);
        return Path.Combine(gameRoot, "Binaries", "Win64", "ue4ss");
    }

    public (string? releaseVersion, string? buildNumber)? GetLatestUe4ssReleaseInfo()
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

            var html = client.GetStringAsync(Ue4ssGithubReleaseUrl).GetAwaiter().GetResult();
            var releaseVersion = ExtractUe4ssReleaseVersionFromGitHub(html);
            var buildNumber = ExtractUe4ssBuildNumberFromGitHub(html);
            if (!string.IsNullOrWhiteSpace(releaseVersion) || !string.IsNullOrWhiteSpace(buildNumber))
            {
                return (releaseVersion, buildNumber);
            }

            AppLogger.Warn("Unable to parse the latest UE4SS release from GitHub.");
            return null;
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to fetch the latest UE4SS version from GitHub.", exception);
            return null;
        }
    }

    public Version? GetLatestModLoaderVersion()
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

            var html = client.GetStringAsync(ModLoaderGithubReleasesUrl).GetAwaiter().GetResult();
            var version = ExtractModLoaderVersionFromGitHub(html);
            if (version is not null)
            {
                return version;
            }

            html = client.GetStringAsync(ModLoaderGithubTagsUrl).GetAwaiter().GetResult();
            return ExtractModLoaderVersionFromGitHub(html);
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to fetch the latest mod loader version from GitHub.", exception);
            return null;
        }
    }

    public Ue4ssInstallStatus InspectUe4ss(string? installPath, string? selectedUpdatePath = null)
    {
        var bundlePath = GetBundledUe4ssArchivePath();
        var installRoot = string.IsNullOrWhiteSpace(installPath) ? null : GetUe4ssInstallRoot(installPath);
        var installDetected = !string.IsNullOrWhiteSpace(installRoot) && Directory.Exists(installRoot);
        var inDirectory = Directory.Exists(AppPaths.Ue4ssDirectory) &&
                          Directory.EnumerateFileSystemEntries(AppPaths.Ue4ssDirectory).Any();
        var binariesRoot = string.IsNullOrWhiteSpace(installPath) ? null : Path.Combine(GetGameRootPath(installPath), "Binaries", "Win64");
        var ue4ssRoot = binariesRoot is null ? null : Path.Combine(binariesRoot, "ue4ss");
        var proxyDllPresent = binariesRoot is not null &&
                              (File.Exists(Path.Combine(binariesRoot, "dwmapi.dll")) ||
                               File.Exists(Path.Combine(binariesRoot, "version.dll")));
        var installed = installDetected &&
                        proxyDllPresent &&
                        ue4ssRoot is not null &&
                        File.Exists(Path.Combine(ue4ssRoot, "UE4SS.dll")) &&
                        Directory.Exists(Path.Combine(ue4ssRoot, "Mods"));
        var bundleFiles = bundlePath is null ? 0 : CountBundleFiles(bundlePath);
        var installedFiles = installed ? CountDirectoryFiles(installRoot!) : 0;
        var selectedFetched = !string.IsNullOrWhiteSpace(selectedUpdatePath) && File.Exists(selectedUpdatePath);
        var localVersionText = GetVersionText(bundlePath);
        var bundleBuildText = GetBuildText(bundlePath);
        var latestReleaseInfo = GetLatestUe4ssReleaseInfo();
        var latestReleaseVersionText = latestReleaseInfo?.releaseVersion;
        var latestReleaseBuildText = latestReleaseInfo?.buildNumber;
        var localVersion = TryParseVersionNumber(localVersionText);
        var latestVersion = TryParseVersionNumber(latestReleaseVersionText);
        var localBuild = TryParseBuildNumber(bundleBuildText);
        var latestBuild = TryParseBuildNumber(latestReleaseBuildText);
        var versionComparison = localVersion is not null && latestVersion is not null
            ? localVersion >= latestVersion
            : null as bool?;
        var buildComparison = localBuild is not null && latestBuild is not null
            ? localBuild >= latestBuild
            : null as bool?;
        var upToDate = versionComparison ?? buildComparison ?? false;

        var statusText = !inDirectory
            ? "UE4SS FOLDER NOT FOUND"
            : !installed
                ? "UE4SS INSTALL BROKEN"
                : latestVersion is null
                    ? "VERSION CHECK UNAVAILABLE"
                    : upToDate
                    ? "UE4SS UP TO DATE"
                    : "UPDATE AVAILABLE";

        var summaryText = bundlePath is null
            ? "The bundled UE4SS archive could not be found."
            : !inDirectory
                ? "No UE4SS folder was found under the game install yet."
                : latestReleaseVersionText is null
                    ? "GitHub release information could not be fetched right now."
                : upToDate
                    ? "Bundled UE4SS matches the latest GitHub release."
                    : $"Latest GitHub release: V{latestReleaseVersionText}.";

        return new Ue4ssInstallStatus
        {
            BundlePath = bundlePath ?? "Missing bundled archive",
            BundleLabel = bundlePath is null ? "No bundled archive found" : Path.GetFileName(bundlePath),
            InstallRoot = installRoot,
            InstallDetected = installDetected,
            Installed = installed,
            UpToDate = upToDate,
            InDirectory = inDirectory,
            VersionFetched = selectedFetched,
            BundleFileCount = bundleFiles,
            InstalledFileCount = installedFiles,
            BundleBuildText = bundleBuildText,
            LatestReleaseVersionText = latestReleaseVersionText,
            LatestReleaseBuildText = latestReleaseBuildText,
            VersionText = localVersionText,
            StatusText = statusText,
            SummaryText = summaryText
        };
    }

    public bool RemoveUe4ssInstall(string installPath)
    {
        try
        {
            var gameRoot = GetGameRootPath(installPath);
            var binariesRoot = Path.Combine(gameRoot, "Binaries", "Win64");
            var ue4ssRoot = Path.Combine(binariesRoot, "ue4ss");
            var removedAnything = false;
            if (Directory.Exists(ue4ssRoot))
            {
                Directory.Delete(ue4ssRoot, true);
                removedAnything = true;
            }
            else
            {
                AppLogger.Warn($"UE4SS directory not found at {ue4ssRoot}.");
            }

            var proxyDll = Path.Combine(binariesRoot, "dwmapi.dll");
            var fallbackDll = Path.Combine(binariesRoot, "version.dll");
            if (File.Exists(proxyDll))
            {
                File.Delete(proxyDll);
                removedAnything = true;
            }

            if (File.Exists(fallbackDll))
            {
                File.Delete(fallbackDll);
                removedAnything = true;
            }

            AppLogger.Info($"Removed UE4SS install from {ue4ssRoot}.");
            return removedAnything;
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to remove UE4SS install.", exception);
            return false;
        }
    }

    public bool ApplyBundledUe4ss(string installPath)
    {
        var bundlePath = GetBundledUe4ssArchivePath();
        if (bundlePath is null)
        {
            AppLogger.Warn("UE4SS bundle archive could not be found.");
            return false;
        }

        return ApplyUe4ssPackage(bundlePath, installPath);
    }

    public bool ApplyUe4ssPackage(string packagePath, string installPath)
    {
        if (!File.Exists(packagePath) && !Directory.Exists(packagePath))
        {
            AppLogger.Warn($"UE4SS package could not be found: {packagePath}.");
            return false;
        }

        var binariesRoot = Path.Combine(GetGameRootPath(installPath), "Binaries", "Win64");
        var ue4ssRoot = Path.Combine(binariesRoot, "ue4ss");
        Directory.CreateDirectory(binariesRoot);
        Directory.CreateDirectory(ue4ssRoot);

        try
        {
            RemoveTarget(Path.Combine(binariesRoot, "dwmapi.dll"));
            RemoveTarget(Path.Combine(binariesRoot, "version.dll"));
            ResetUe4ssCore(ue4ssRoot);

            var extractedPath = PreparePackageSource(packagePath);
            if (extractedPath is null)
            {
                return false;
            }

            var sourceRoot = ResolvePackageRoot(extractedPath) ?? extractedPath;
            CopyDirectory(sourceRoot, binariesRoot);
            AppLogger.Info($"Applied UE4SS package from {packagePath} to {ue4ssRoot}.");
            return File.Exists(Path.Combine(binariesRoot, "dwmapi.dll")) &&
                   File.Exists(Path.Combine(ue4ssRoot, "UE4SS.dll")) &&
                   Directory.Exists(Path.Combine(ue4ssRoot, "Mods"));
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed applying UE4SS package.", exception);
            return false;
        }
    }

    public PackageType DetectPackageType(string sourcePath)
    {
        var fileName = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.Equals(fileName, "_UE4SS", StringComparison.OrdinalIgnoreCase))
        {
            return PackageType.Ue4ss;
        }

        var (files, archiveSupported) = EnumerateSourceFiles(sourcePath);
        if (!archiveSupported && IsArchive(sourcePath))
        {
            return PackageType.Unknown;
        }

        var hasUe4ss = files.Any(IsUe4ssMarker);
        var hasPak = files.Any(IsPakAsset);

        if (hasUe4ss && hasPak)
        {
            return PackageType.Mixed;
        }

        if (hasUe4ss)
        {
            return PackageType.Ue4ss;
        }

        if (hasPak)
        {
            return PackageType.Pak;
        }

        return PackageType.Unknown;
    }

    public string GetDeploymentTargetPath(ModEntry entry, string installPath)
    {
        var packageType = DetectPackageType(entry.SourcePath);
        var gameRoot = GetGameRootPath(installPath);
        var packageName = GetPackageName(entry.SourcePath);

        return packageType switch
        {
            PackageType.Pak => Path.Combine(gameRoot, "Content", "Paks"),
            PackageType.Ue4ss => Path.Combine(gameRoot, "Binaries", "Win64", "ue4ss", "Mods", packageName),
            PackageType.Mixed => $"{Path.Combine(gameRoot, "Binaries", "Win64", "ue4ss", "Mods", packageName)} | {Path.Combine(gameRoot, "Content", "Paks")}",
            _ => Path.Combine(gameRoot, "Binaries", "Win64", "ue4ss", "Mods", packageName)
        };
    }

    public bool IsDeployed(ModEntry entry, string? installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return false;
        }

        var packageType = DetectPackageType(entry.SourcePath);
        var gameRoot = GetGameRootPath(installPath);
        var packageName = GetPackageName(entry.SourcePath);

        return packageType switch
        {
            PackageType.Pak => Directory.Exists(Path.Combine(gameRoot, "Content", "Paks")) &&
                               EnumerateSourceFiles(entry.SourcePath).files.Any(IsPakAsset) &&
                               EnumerateSourceFiles(entry.SourcePath).files.Where(IsPakAsset).Any(file =>
                               {
                                   var fileName = Path.GetFileName(file);
                                   var paksRoot = Path.Combine(gameRoot, "Content", "Paks");
                                   return File.Exists(Path.Combine(paksRoot, fileName)) ||
                                          File.Exists(Path.Combine(paksRoot, $"{fileName}.disabled"));
                               }),
            PackageType.Ue4ss => Directory.Exists(Path.Combine(gameRoot, "Binaries", "Win64", "ue4ss", "Mods", packageName)),
            PackageType.Mixed => IsUe4ssDeployed(entry.SourcePath, gameRoot, packageName) && IsPakDeployed(entry.SourcePath, gameRoot),
            _ => false
        };
    }

    public string GetPackageTypeLabel(string sourcePath)
    {
        return DetectPackageType(sourcePath) switch
        {
            PackageType.Pak => "PAK MOD",
            PackageType.Ue4ss => "UE4SS MOD",
            PackageType.Mixed => "PAK / UE4SS MOD",
            _ => "UNKNOWN MOD"
        };
    }

    public void SetDeploymentState(ModEntry entry, string installPath, bool apply)
    {
        var gameRoot = GetGameRootPath(installPath);
        var packageType = DetectPackageType(entry.SourcePath);

        if (packageType == PackageType.Unknown)
        {
            AppLogger.Warn($"Skipping unsupported package type for {entry.DisplayName}.");
            entry.IsCopiedToGameFolder = false;
            return;
        }

        if (apply)
        {
            DeployPackage(entry, gameRoot, packageType);
            AppLogger.Info($"Applied {entry.DisplayName} as {packageType}.");
        }
        else
        {
            RemovePackage(entry, gameRoot, packageType);
            AppLogger.Info($"Removed {entry.DisplayName} from game folders.");
        }

        entry.IsCopiedToGameFolder = IsDeployed(entry, installPath);
    }

    public bool SetDeploymentEnabled(ModEntry entry, string installPath, bool enabled)
    {
        if (!IsDeployed(entry, installPath))
        {
            AppLogger.Warn($"Toggle requested for {entry.DisplayName} but it is not applied.");
            return false;
        }

        var gameRoot = GetGameRootPath(installPath);
        var packageType = DetectPackageType(entry.SourcePath);

        if (packageType is PackageType.Ue4ss or PackageType.Mixed)
        {
            SetUe4ssEnabledState(entry, gameRoot, enabled);
        }

        if (packageType is PackageType.Pak or PackageType.Mixed)
        {
            SetPakEnabledState(entry, gameRoot, enabled);
        }

        entry.IsEnabled = enabled;
        entry.IsCopiedToGameFolder = IsDeployed(entry, installPath);
        AppLogger.Info($"{(enabled ? "Enabled" : "Disabled")} deployed mod {entry.DisplayName}.");
        return true;
    }

    public string GetLogicalDeploymentDisplayPath(ModEntry entry, bool enabled)
    {
        var packageType = DetectPackageType(entry.SourcePath);
        var packageName = GetPackageName(entry.SourcePath);

        return packageType switch
        {
            PackageType.Pak => $"{packageName}{(enabled ? string.Empty : ".disabled")}",
            PackageType.Ue4ss => enabled ? "enabled.txt" : "disabled.txt",
            PackageType.Mixed => $"UE4SS:{(enabled ? "enabled.txt" : "disabled.txt")} | PAK:{packageName}{(enabled ? string.Empty : ".disabled")}",
            _ => packageName
        };
    }

    public void ApplyDeployments(IEnumerable<ModEntry> entries, string installPath)
    {
        var gameRoot = GetGameRootPath(installPath);
        var paksRoot = Path.Combine(gameRoot, "Content", "Paks");
        var ue4ssModsRoot = Path.Combine(gameRoot, "Binaries", "Win64", "ue4ss", "Mods");
        Directory.CreateDirectory(paksRoot);
        Directory.CreateDirectory(ue4ssModsRoot);

        foreach (var entry in entries.Where(entry => entry.Id != "placeholder:no-mods"))
        {
            var packageType = DetectPackageType(entry.SourcePath);
            if (packageType == PackageType.Unknown)
            {
                AppLogger.Warn($"Skipping unsupported package type for {entry.DisplayName}.");
                entry.IsCopiedToGameFolder = false;
                continue;
            }

            if (entry.IsBundled || entry.IsEnabled)
            {
                DeployPackage(entry, gameRoot, packageType);
                entry.IsCopiedToGameFolder = IsDeployed(entry, installPath);
                AppLogger.Info($"Applied {entry.DisplayName} as {packageType}.");
            }
            else
            {
                RemovePackage(entry, gameRoot, packageType);
                entry.IsCopiedToGameFolder = IsDeployed(entry, installPath);
                AppLogger.Info($"Removed {entry.DisplayName} from game folders.");
            }
        }
    }

    private static IEnumerable<string> EnumerateSteamRoots()
    {
        var roots = new List<string>();

        AddRegistryValue(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath", roots);
        AddRegistryValue(Registry.CurrentUser, @"Software\Valve\Steam", "InstallPath", roots);
        AddRegistryValue(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", roots);
        AddRegistryValue(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath", roots);

        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));

        var parsedRoots = new List<string>();
        foreach (var root in roots.Where(root => !string.IsNullOrWhiteSpace(root)))
        {
            var normalizedRoot = root.Trim().Trim('"');
            if (Directory.Exists(normalizedRoot))
            {
                parsedRoots.Add(normalizedRoot);
            }

            var libraryFile = Path.Combine(normalizedRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFile))
            {
                continue;
            }

            try
            {
                var text = File.ReadAllText(libraryFile);
                foreach (Match match in Regex.Matches(text, @"([A-Za-z]:\\\\[^""]+|[A-Za-z]:\\[^""]+)"))
                {
                    var libraryPath = match.Value.Replace("\\\\", "\\");
                    if (Directory.Exists(libraryPath))
                    {
                        parsedRoots.Add(libraryPath);
                    }
                }
            }
            catch (Exception exception)
            {
                AppLogger.Exception($"Failed reading Steam library file at {libraryFile}.", exception);
            }
        }

        return parsedRoots;
    }

    private static void AddRegistryValue(RegistryKey? root, string subKeyPath, string valueName, ICollection<string> roots)
    {
        try
        {
            using var key = root?.OpenSubKey(subKeyPath);
            var value = key?.GetValue(valueName) as string;
            if (!string.IsNullOrWhiteSpace(value))
            {
                roots.Add(value);
            }
        }
        catch (Exception exception)
        {
            AppLogger.Exception($"Failed reading registry value {subKeyPath}\\{valueName}.", exception);
        }
    }

    private static string? FindInstallUnderSteamRoot(string steamRoot)
    {
        var commonRoot = Path.Combine(steamRoot, "steamapps", "common");
        if (!Directory.Exists(commonRoot))
        {
            return null;
        }

        foreach (var directory in Directory.EnumerateDirectories(commonRoot, "*", SearchOption.TopDirectoryOnly))
        {
            var folderName = Path.GetFileName(directory);
            if (folderName.Contains("Abiotic", StringComparison.OrdinalIgnoreCase) ||
                folderName.Contains("Factor", StringComparison.OrdinalIgnoreCase))
            {
                var directInstall = TryResolveGameRoot(directory);
                if (!string.IsNullOrWhiteSpace(directInstall))
                {
                    return directInstall;
                }
            }
        }

        try
        {
            foreach (var executable in Directory.EnumerateFiles(commonRoot, ShippingExecutableName, SearchOption.AllDirectories))
            {
                var resolved = TryResolveGameRoot(Path.GetDirectoryName(executable) ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return resolved;
                }
            }
        }
        catch (Exception exception)
        {
            AppLogger.Exception($"Failed searching Steam common folder at {commonRoot}.", exception);
        }

        return null;
    }

    private static string? TryResolveGameRoot(string candidateDirectory)
    {
        if (string.IsNullOrWhiteSpace(candidateDirectory))
        {
            return null;
        }

        var binariesDirectory = Path.Combine(candidateDirectory, "Binaries", "Win64");
        var shippingExe = Path.Combine(binariesDirectory, ShippingExecutableName);
        if (File.Exists(shippingExe))
        {
            return candidateDirectory;
        }

        var parent = Directory.GetParent(candidateDirectory);
        if (parent is not null && string.Equals(parent.Name, "Win64", StringComparison.OrdinalIgnoreCase))
        {
            var binariesParent = parent.Parent;
            if (binariesParent is not null && string.Equals(binariesParent.Name, "Binaries", StringComparison.OrdinalIgnoreCase))
            {
                return binariesParent.Parent?.FullName ?? candidateDirectory;
            }
        }

        if (File.Exists(Path.Combine(candidateDirectory, ShippingExecutableName)))
        {
            return candidateDirectory;
        }

        return null;
    }

    private void DeployPackage(ModEntry entry, string gameRoot, PackageType packageType)
    {
        if (packageType is PackageType.Ue4ss or PackageType.Mixed)
        {
            DeployUe4ss(entry, gameRoot);
        }

        if (packageType is PackageType.Pak or PackageType.Mixed)
        {
            DeployPaks(entry, gameRoot);
        }
    }

    private void RemovePackage(ModEntry entry, string gameRoot, PackageType packageType)
    {
        if (packageType is PackageType.Ue4ss or PackageType.Mixed)
        {
            RemoveUe4ss(entry, gameRoot);
        }

        if (packageType is PackageType.Pak or PackageType.Mixed)
        {
            RemovePaks(entry, gameRoot);
        }
    }

    private void DeployUe4ss(ModEntry entry, string gameRoot)
    {
        var modsRoot = Path.Combine(gameRoot, "Binaries", "Win64", "ue4ss", "Mods");
        Directory.CreateDirectory(modsRoot);

        var extractedPath = PreparePackageSource(entry.SourcePath);
        if (extractedPath is null)
        {
            return;
        }

        var packageName = GetPackageName(entry.SourcePath);
        var sourceRoot = ResolvePackageRoot(extractedPath) ?? extractedPath;
        var destinationPath = Path.Combine(modsRoot, packageName);

        RemoveTarget(destinationPath);
        CopyDirectory(sourceRoot, destinationPath);
        SetUe4ssEnabledState(entry, gameRoot, entry.IsEnabled);
    }

    private void RemoveUe4ss(ModEntry entry, string gameRoot)
    {
        var modsRoot = Path.Combine(gameRoot, "Binaries", "Win64", "ue4ss", "Mods");
        var destinationPath = Path.Combine(modsRoot, GetPackageName(entry.SourcePath));
        RemoveTarget(destinationPath);
    }

    private void DeployPaks(ModEntry entry, string gameRoot)
    {
        var paksRoot = Path.Combine(gameRoot, "Content", "Paks");
        Directory.CreateDirectory(paksRoot);

        var extractedPath = PreparePackageSource(entry.SourcePath);
        if (extractedPath is null)
        {
            return;
        }

        foreach (var file in EnumerateSourceFiles(extractedPath).files.Where(IsPakAsset))
        {
            var targetName = entry.IsEnabled ? Path.GetFileName(file) : $"{Path.GetFileName(file)}.disabled";
            var targetPath = Path.Combine(paksRoot, targetName);
            File.Copy(file, targetPath, true);
        }
    }

    private void RemovePaks(ModEntry entry, string gameRoot)
    {
        var paksRoot = Path.Combine(gameRoot, "Content", "Paks");
        if (!Directory.Exists(paksRoot))
        {
            return;
        }

        foreach (var file in EnumerateSourceFiles(entry.SourcePath).files.Where(IsPakAsset))
        {
            var baseName = Path.GetFileName(file);
            var targetPath = Path.Combine(paksRoot, baseName);
            var disabledPath = Path.Combine(paksRoot, $"{baseName}.disabled");
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            if (File.Exists(disabledPath))
            {
                File.Delete(disabledPath);
            }
        }
    }

    private static string GetPackageName(string sourcePath)
    {
        return Directory.Exists(sourcePath)
            ? Path.GetFileName(Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : Path.GetFileNameWithoutExtension(sourcePath);
    }

    private static (IReadOnlyList<string> files, bool archiveSupported) EnumerateSourceFiles(string sourcePath)
    {
        if (Directory.Exists(sourcePath))
        {
            return (Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories).ToList(), true);
        }

        if (!File.Exists(sourcePath))
        {
            return (Array.Empty<string>(), true);
        }

        if (Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(sourcePath);
            return (archive.Entries.Where(entry => !string.IsNullOrWhiteSpace(entry.Name)).Select(entry => entry.FullName).ToList(), true);
        }

        if (IsArchive(sourcePath))
        {
            return (Array.Empty<string>(), false);
        }

        return (new[] { sourcePath }, true);
    }

    private static bool IsArchive(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        return extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".7z", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".rar", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUe4ssMarker(string filePath)
    {
        var normalized = filePath.Replace('/', Path.DirectorySeparatorChar);
        var fileName = Path.GetFileName(normalized);
        return normalized.Contains($"{Path.DirectorySeparatorChar}Scripts{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith($"Scripts{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains($"{Path.DirectorySeparatorChar}dlls{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith($"dlls{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("enabled.txt", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("main.dll", StringComparison.OrdinalIgnoreCase) ||
               Path.GetExtension(fileName).Equals(".lua", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPakAsset(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".pak", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".utoc", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ucas", StringComparison.OrdinalIgnoreCase);
    }

    private string? PreparePackageSource(string sourcePath)
    {
        if (Directory.Exists(sourcePath))
        {
            return sourcePath;
        }

        if (!File.Exists(sourcePath))
        {
            return null;
        }

        if (Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var destination = Path.Combine(AppPaths.TempDirectory, "Extracted", Path.GetFileNameWithoutExtension(sourcePath), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(destination);
            ZipFile.ExtractToDirectory(sourcePath, destination, true);
            return destination;
        }

        if (Path.GetExtension(sourcePath).Equals(".7z", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(sourcePath).Equals(".rar", StringComparison.OrdinalIgnoreCase))
        {
            var destination = Path.Combine(AppPaths.TempDirectory, "Extracted", Path.GetFileNameWithoutExtension(sourcePath), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(destination);

            if (TryInvoke7Zip(sourcePath, destination))
            {
                return destination;
            }

            AppLogger.Warn($"Unable to extract archive {sourcePath}; 7-Zip was not available.");
            return null;
        }

        return sourcePath;
    }

    private static bool TryInvoke7Zip(string archivePath, string destinationPath)
    {
        var candidates = new[]
        {
            "7z.exe",
            "7za.exe",
            "7z"
        };

        foreach (var candidate in candidates)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = $"x -y \"{archivePath}\" -o\"{destinationPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process is null)
                {
                    continue;
                }

                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    return true;
                }
            }
            catch
            {
                // Try the next candidate.
            }
        }

        return false;
    }

    private static string? ResolvePackageRoot(string extractedPath)
    {
        var directories = Directory.EnumerateDirectories(extractedPath, "*", SearchOption.TopDirectoryOnly).ToList();
        var files = Directory.EnumerateFiles(extractedPath, "*", SearchOption.TopDirectoryOnly).ToList();
        if (directories.Count == 1 && files.Count == 0)
        {
            return directories[0];
        }

        return extractedPath;
    }

    private static void UpdateModsTxt(string modsRoot, string packageName, bool addEntry)
    {
        var modsTxtPath = Path.Combine(modsRoot, "mods.txt");
        Directory.CreateDirectory(modsRoot);

        var lines = File.Exists(modsTxtPath)
            ? File.ReadAllLines(modsTxtPath).ToList()
            : [];

        var matchingIndex = lines.FindIndex(line => line.StartsWith(packageName, StringComparison.OrdinalIgnoreCase));
        if (addEntry)
        {
            var entryLine = $"{packageName} : 1";
            if (matchingIndex >= 0)
            {
                lines[matchingIndex] = entryLine;
            }
            else
            {
                lines.Add(entryLine);
            }
        }
        else if (matchingIndex >= 0)
        {
            lines.RemoveAt(matchingIndex);
        }

        File.WriteAllLines(modsTxtPath, lines);
    }

    private static void RenameIfExists(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        File.Move(sourcePath, destinationPath);
    }

    private static void SetUe4ssEnabledState(ModEntry entry, string gameRoot, bool enabled)
    {
        var modsRoot = Path.Combine(gameRoot, "Binaries", "Win64", "ue4ss", "Mods", GetPackageName(entry.SourcePath));
        if (!Directory.Exists(modsRoot))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(modsRoot, "*", SearchOption.AllDirectories).ToList())
        {
            var fileName = Path.GetFileName(file);
            if (enabled && fileName.Equals("disabled.txt", StringComparison.OrdinalIgnoreCase))
            {
                RenameIfExists(file, Path.Combine(Path.GetDirectoryName(file) ?? modsRoot, "enabled.txt"));
            }
            else if (!enabled && fileName.Equals("enabled.txt", StringComparison.OrdinalIgnoreCase))
            {
                RenameIfExists(file, Path.Combine(Path.GetDirectoryName(file) ?? modsRoot, "disabled.txt"));
            }
        }
    }

    private static void SetPakEnabledState(ModEntry entry, string gameRoot, bool enabled)
    {
        var paksRoot = Path.Combine(gameRoot, "Content", "Paks");
        if (!Directory.Exists(paksRoot))
        {
            return;
        }

        foreach (var file in EnumerateSourceFiles(entry.SourcePath).files.Where(IsPakAsset).ToList())
        {
            var baseName = Path.GetFileName(file);
            var enabledPath = Path.Combine(paksRoot, baseName);
            var disabledPath = Path.Combine(paksRoot, $"{baseName}.disabled");

            if (enabled)
            {
                RenameIfExists(disabledPath, enabledPath);
            }
            else
            {
                RenameIfExists(enabledPath, disabledPath);
            }
        }
    }

    private static int CountBundleFiles(string archivePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            return archive.Entries.Count(entry => !string.IsNullOrWhiteSpace(entry.Name) &&
                                                  !IsIgnoredUe4ssEntry(entry.FullName));
        }
        catch (Exception exception)
        {
            AppLogger.Exception($"Failed counting bundled UE4SS files in {archivePath}.", exception);
            return 0;
        }
    }

    private static int CountDirectoryFiles(string directoryPath)
    {
        try
        {
            return Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
                .Count(path => !IsIgnoredUe4ssPath(Path.GetRelativePath(directoryPath, path)));
        }
        catch (Exception exception)
        {
            AppLogger.Exception($"Failed counting UE4SS files in {directoryPath}.", exception);
            return 0;
        }
    }

    private static string ComputePackageSignature(string packagePath)
    {
        if (Directory.Exists(packagePath))
        {
            return ComputeDirectorySignature(packagePath);
        }

        using var sha = SHA256.Create();

        using var archive = ZipFile.OpenRead(packagePath);

        foreach (var entry in archive.Entries
                     .Where(entry => !string.IsNullOrWhiteSpace(entry.Name) && !IsIgnoredUe4ssEntry(entry.FullName))
                     .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase))
        {
            var pathBytes = Encoding.UTF8.GetBytes(entry.FullName.ToLowerInvariant());
            sha.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

            using var stream = entry.Open();
            var buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                sha.TransformBlock(buffer, 0, read, null, 0);
            }
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash ?? Array.Empty<byte>());
    }

    private static string ComputeDirectorySignature(string directoryPath)
    {
        using var sha = SHA256.Create();

        foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
                     .Where(path => !IsIgnoredUe4ssPath(Path.GetRelativePath(directoryPath, path)))
                     .OrderBy(path => Path.GetRelativePath(directoryPath, path), StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(directoryPath, file).Replace(Path.DirectorySeparatorChar, '/').ToLowerInvariant();
            var pathBytes = Encoding.UTF8.GetBytes(relativePath);
            sha.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

            using var stream = File.OpenRead(file);
            var buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                sha.TransformBlock(buffer, 0, read, null, 0);
            }
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash ?? Array.Empty<byte>());
    }

    private static bool IsIgnoredUe4ssEntry(string entryPath)
    {
        var normalized = entryPath.Replace('\\', '/');
        return normalized.StartsWith("ue4ss/Mods/", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "ue4ss/Mods", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIgnoredUe4ssPath(string relativePath)
    {
        var normalized = relativePath.Replace(Path.DirectorySeparatorChar, '/');
        return normalized.StartsWith("Mods/", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "Mods", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetVersionText(string? packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return "Vunknown";
        }

        var fileName = Path.GetFileNameWithoutExtension(packagePath);
        var version = TryNormalizeVersionFromFileName(fileName);
        return version is null ? $"V{fileName}" : $"V{version}";
    }

    private static string GetBuildText(string? packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return "unknown";
        }

        var fileName = Path.GetFileNameWithoutExtension(packagePath);
        var version = TryNormalizeBuildFromFileName(fileName);
        return version ?? "unknown";
    }

    private static string? TryNormalizeVersionFromFileName(string fileName)
    {
        var bundleMatch = Regex.Match(
            fileName,
            @"-(?<major>\d+)-(?<minor>\d+)-(?<patch>\d+)-\d+$",
            RegexOptions.IgnoreCase);
        if (bundleMatch.Success)
        {
            return $"{bundleMatch.Groups["major"].Value}.{bundleMatch.Groups["minor"].Value}.{bundleMatch.Groups["patch"].Value}";
        }

        var archiveMatch = Regex.Match(
            fileName,
            @"(?:(?:UE4SS[_-]v)?\d+(?:\.\d+)*[_-])?(?<major>\d+)-(?<minor>\d+)-(?<patch>\d+)-\d+$",
            RegexOptions.IgnoreCase);
        if (archiveMatch.Success)
        {
            return $"{archiveMatch.Groups["major"].Value}.{archiveMatch.Groups["minor"].Value}.{archiveMatch.Groups["patch"].Value}";
        }

        var semverMatch = Regex.Match(fileName, @"(?<!\d)(?<version>\d+\.\d+\.\d+)(?!\d)");
        if (semverMatch.Success)
        {
            return semverMatch.Groups["version"].Value;
        }

        return null;
    }

    private static string? TryNormalizeBuildFromFileName(string fileName)
    {
        var bundleMatch = Regex.Match(
            fileName,
            @"(?:UE4SS[_-]v)?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)-(?<build>\d+)-g[0-9a-f]+",
            RegexOptions.IgnoreCase);
        if (bundleMatch.Success)
        {
            return bundleMatch.Groups["build"].Value;
        }

        var buildMatch = Regex.Match(fileName, @"(?<!\d)(?<build>\d{3,5})(?!\d)");
        return buildMatch.Success ? buildMatch.Groups["build"].Value : null;
    }

    private static string? ExtractUe4ssReleaseVersionFromGitHub(string html)
    {
        var preferredMatches = new[]
        {
            Regex.Match(html, @"Bundle\s+v(?<version>\d+\.\d+\.\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline),
            Regex.Match(html, @"releases/tag/(?<version>\d+\.\d+\.\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline)
        };

        foreach (var match in preferredMatches)
        {
            if (match.Success)
            {
                return match.Groups["version"].Value;
            }
        }

        return null;
    }

    private static string? ExtractUe4ssBuildNumberFromGitHub(string html)
    {
        var matches = Regex.Matches(
            html,
            @"UE4SS\s+v(?:\d+\.\d+\.\d+)-(?<build>\d+)-g[0-9a-f]+",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        int? highestBuild = null;
        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            if (int.TryParse(match.Groups["build"].Value, out var build))
            {
                highestBuild = highestBuild is null ? build : Math.Max(highestBuild.Value, build);
            }
        }

        return highestBuild?.ToString();
    }

    private static Version? TryParseVersionFromText(string? versionText)
    {
        return Version.TryParse(versionText, out var version) ? version : null;
    }

    private static int? TryParseInteger(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static int? TryParseBuildNumber(string? buildText)
    {
        if (string.IsNullOrWhiteSpace(buildText))
        {
            return null;
        }

        return int.TryParse(buildText, out var build) ? build : null;
    }

    private static Version? TryParseVersionNumber(string? versionText)
    {
        if (string.IsNullOrWhiteSpace(versionText))
        {
            return null;
        }

        var match = Regex.Match(versionText, @"(?<version>\d+\.\d+\.\d+)");
        if (!match.Success)
        {
            return null;
        }

        return Version.TryParse(match.Groups["version"].Value, out var version) ? version : null;
    }

    private static Version? ExtractModLoaderVersionFromGitHub(string html)
    {
        var matches = Regex.Matches(html, @"(?i)(?:releases/tag/|/tag/)(?:v)?(?<version>\d+(?:\.\d+){1,3})");
        foreach (Match match in matches)
        {
            if (Version.TryParse(match.Groups["version"].Value, out var version))
            {
                return version;
            }
        }

        return null;
    }

    private static bool IsUe4ssDeployed(string sourcePath, string gameRoot, string packageName)
    {
        var modsRoot = Path.Combine(gameRoot, "Binaries", "Win64", "ue4ss", "Mods", packageName);
        return Directory.Exists(modsRoot);
    }

    private static bool IsPakDeployed(string sourcePath, string gameRoot)
    {
        var paksRoot = Path.Combine(gameRoot, "Content", "Paks");
        if (!Directory.Exists(paksRoot))
        {
            return false;
        }

        var (files, _) = EnumerateSourceFiles(sourcePath);
        return files.Where(IsPakAsset).Any(file => File.Exists(Path.Combine(paksRoot, Path.GetFileName(file))));
    }

    private static void RemoveTarget(string targetPath)
    {
        if (Directory.Exists(targetPath))
        {
            Directory.Delete(targetPath, true);
        }
        else if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }
    }

    private static void ResetUe4ssCore(string ue4ssRoot)
    {
        if (!Directory.Exists(ue4ssRoot))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(ue4ssRoot, "*", SearchOption.TopDirectoryOnly))
        {
            File.Delete(file);
        }

        foreach (var directory in Directory.EnumerateDirectories(ue4ssRoot, "*", SearchOption.TopDirectoryOnly)
                     .Where(directory => !string.Equals(Path.GetFileName(directory), "Mods", StringComparison.OrdinalIgnoreCase)))
        {
            Directory.Delete(directory, true);
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationDirectory);
            File.Copy(file, destinationPath, true);
        }
    }
}
