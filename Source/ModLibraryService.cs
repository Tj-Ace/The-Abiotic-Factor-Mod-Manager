using System.IO;
using System.Text.RegularExpressions;

namespace AbioticLoader;

public sealed class ModLibraryService
{
    private readonly GameInstallService _gameInstallService = new();

    public IReadOnlyList<ModEntry> Scan(AppState state)
    {
        Directory.CreateDirectory(AppPaths.ModsDirectory);

        var entries = new List<ModEntry>();

        foreach (var path in Directory.EnumerateFileSystemEntries(AppPaths.ModsDirectory))
        {
            if (string.Equals(path, AppPaths.StateFilePath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, AppPaths.LegacyStateFilePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileName = Path.GetFileName(path);
            var isDirectory = Directory.Exists(path);
            var isBundled = isDirectory &&
                            (string.Equals(fileName, "_UE4SS", StringComparison.OrdinalIgnoreCase) ||
                             path.Contains($"{Path.DirectorySeparatorChar}_UE4SS", StringComparison.OrdinalIgnoreCase));
            var kind = isDirectory ? "FOLDER" : Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(kind))
            {
                kind = "FILE";
            }

            var itemCount = isDirectory ? Directory.EnumerateFileSystemEntries(path).Count() : 0;

            var entry = new ModEntry(
                NormalizeId(path),
                isBundled ? "UE4SS" : NormalizeDisplayName(Path.GetFileNameWithoutExtension(fileName)),
                path,
                kind,
                isBundled
                    ? "Bundled UE4SS package. Keep this folder present so mods can load."
                    : isDirectory
                        ? $"Folder package with {itemCount} item(s). Drop DLLs, configs, or content here."
                        : "Package file ready for the loader.",
                isBundled);

            if (!entry.IsBundled)
            {
                entry.IsEnabled = !state.DisabledModIds.Contains(entry.Id);
            }

            entry.IsCopiedToGameFolder = _gameInstallService.IsDeployed(entry, state.GameInstallPath);

            entries.Add(entry);
        }

        if (entries.Count == 0)
        {
            entries.Add(new ModEntry(
                "placeholder:no-mods",
                "NO MODS FOUND",
                AppPaths.ModsDirectory,
                "SYSTEM",
                "Put mod folders or packages in the Mods folder to manage them here.",
                isBundled: true));
        }

        var ordered = entries
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Kind, StringComparer.OrdinalIgnoreCase)
            .ToList();

        AppLogger.Info($"Scanned {ordered.Count} mod entry(s) from {AppPaths.ModsDirectory}.");
        return ordered;
    }

    public void SaveState(AppState state, IEnumerable<ModEntry> entries)
    {
        state.DisabledModIds = entries
            .Where(entry => entry.CanToggle && !entry.IsEnabled)
            .Select(entry => entry.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeId(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string NormalizeDisplayName(string value)
    {
        var cleaned = Regex.Replace(
            value.Trim(),
            @"(?i)(?:[\s._-]*v?\d+(?:\.\d+){1,4})+$",
            string.Empty);

        cleaned = cleaned.Trim(' ', '.', '-', '_');
        return string.IsNullOrWhiteSpace(cleaned) ? value : cleaned;
    }
}
