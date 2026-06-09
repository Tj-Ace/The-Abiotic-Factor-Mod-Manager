using System.IO;

namespace AbioticLoader;

public static class AppBootstrap
{
    public static void EnsureStructure()
    {
        Directory.CreateDirectory(AppPaths.ApplicationRoot);
        Directory.CreateDirectory(AppPaths.ProgramFilesRoot);
        Directory.CreateDirectory(AppPaths.AssetsDirectory);
        Directory.CreateDirectory(AppPaths.SourceDirectory);
        Directory.CreateDirectory(AppPaths.TempDirectory);
        Directory.CreateDirectory(AppPaths.LogsDirectory);
        Directory.CreateDirectory(AppPaths.ModsDirectory);
    }
}
