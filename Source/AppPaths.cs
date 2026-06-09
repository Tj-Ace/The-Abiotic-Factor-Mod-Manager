using System.IO;

namespace AbioticLoader;

public static class AppPaths
{
    public static string ApplicationRoot => LocateApplicationRoot();

    public static string ProgramFilesRoot => Path.Combine(ApplicationRoot, "!Program Files");

    public static string AssetsDirectory => Path.Combine(ProgramFilesRoot, "Assets");

    public static string SourceDirectory => Path.Combine(ProgramFilesRoot, "Source");

    public static string Ue4ssDirectory => Path.Combine(ProgramFilesRoot, "UE4SS");

    public static string TempDirectory => Path.Combine(ProgramFilesRoot, "Temp");

    public static string LogsDirectory => Path.Combine(TempDirectory, "Logs");

    public static string ModsDirectory => Path.Combine(ApplicationRoot, "Mods");

    public static string StateFilePath => Path.Combine(TempDirectory, "loader-state.json");

    public static string LegacyStateFilePath => Path.Combine(ModsDirectory, "loader-state.json");

    public static string CurrentLogFilePath => Path.Combine(LogsDirectory, $"AbioticLoader_{DateTime.Now:yyyyMMdd_HHmmss}.log");

    private static string LocateApplicationRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidateRoot = directory.FullName;
            if (Directory.Exists(Path.Combine(candidateRoot, "!Program Files")) &&
                Directory.Exists(Path.Combine(candidateRoot, "Mods")))
            {
                return candidateRoot;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
