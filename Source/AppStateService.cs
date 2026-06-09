using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbioticLoader;

public sealed class AppStateService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public AppState Load()
    {
        MigrateLegacyStateFile();

        if (!File.Exists(AppPaths.StateFilePath))
        {
            AppLogger.Info("State file not found; starting with empty state.");
            return new AppState();
        }

        try
        {
            var json = File.ReadAllText(AppPaths.StateFilePath);
            var state = JsonSerializer.Deserialize<AppState>(json, SerializerOptions) ?? new AppState();
            AppLogger.Info($"State loaded from {AppPaths.StateFilePath}.");
            return state;
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to load state file.", exception);
            return new AppState();
        }
    }

    public void MigrateLegacyStateFile()
    {
        try
        {
            if (File.Exists(AppPaths.LegacyStateFilePath) && !File.Exists(AppPaths.StateFilePath))
            {
                Directory.CreateDirectory(AppPaths.TempDirectory);
                File.Move(AppPaths.LegacyStateFilePath, AppPaths.StateFilePath);
                AppLogger.Info($"Migrated legacy state file to {AppPaths.StateFilePath}.");
            }
            else if (File.Exists(AppPaths.LegacyStateFilePath))
            {
                File.Delete(AppPaths.LegacyStateFilePath);
                AppLogger.Info($"Removed legacy state file at {AppPaths.LegacyStateFilePath}.");
            }
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to migrate legacy state file.", exception);
        }
    }

    public void Save(AppState state)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.TempDirectory);
            var json = JsonSerializer.Serialize(state, SerializerOptions);
            File.WriteAllText(AppPaths.StateFilePath, json);
            AppLogger.Info($"State saved to {AppPaths.StateFilePath}.");
        }
        catch (Exception exception)
        {
            AppLogger.Exception("Failed to save state file.", exception);
        }
    }
}
