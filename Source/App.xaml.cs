using System.Windows;

namespace AbioticLoader;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            AppBootstrap.EnsureStructure();
            AppLogger.Initialize();
            AppLogger.Info("Application starting.");
            AppLogger.Info($"Executable base directory: {AppContext.BaseDirectory}");
            AppLogger.Info($"Application root: {AppPaths.ApplicationRoot}");
            AppLogger.Info($"Mods directory: {AppPaths.ModsDirectory}");
            AppLogger.Info($"Temp directory: {AppPaths.TempDirectory}");

            var stateService = new AppStateService();
            stateService.MigrateLegacyStateFile();

            DispatcherUnhandledException += (_, args) =>
            {
                AppLogger.Exception("Dispatcher unhandled exception.", args.Exception);
                args.Handled = true;
                ShutdownAndExit(1);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception exception)
                {
                    AppLogger.Exception("AppDomain unhandled exception.", exception);
                }

                ShutdownAndExit(1);
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                AppLogger.Exception("Task unobserved exception.", args.Exception);
                args.SetObserved();
            };

            Exit += (_, _) => AppLogger.Info("Application exiting.");

            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            try
            {
                AppLogger.Exception("Fatal startup exception.", exception);
            }
            catch
            {
                // If logging fails, still terminate below.
            }

            ShutdownAndExit(1);
        }
    }

    private static void ShutdownAndExit(int exitCode)
    {
        try
        {
            Current?.Shutdown(exitCode);
        }
        catch
        {
            // Ignore and fall through to hard exit.
        }
        finally
        {
            Environment.Exit(exitCode);
        }
    }
}
