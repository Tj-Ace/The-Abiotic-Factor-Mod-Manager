using System.IO;
using System.Text;

namespace AbioticLoader;

public static class AppLogger
{
    private static readonly object SyncRoot = new();
    private static StreamWriter? _writer;
    private static string _logFilePath = string.Empty;

    public static string LogFilePath
    {
        get
        {
            lock (SyncRoot)
            {
                return _logFilePath;
            }
        }
    }

    public static void Initialize()
    {
        lock (SyncRoot)
        {
            Directory.CreateDirectory(AppPaths.LogsDirectory);
            _logFilePath = AppPaths.CurrentLogFilePath;
            _writer = new StreamWriter(new FileStream(_logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read), Encoding.UTF8)
            {
                AutoFlush = true
            };

            WriteLine("INFO", "Logger initialized.");
            WriteLine("INFO", $"Log file: {_logFilePath}");
        }
    }

    public static void Info(string message) => WriteLine("INFO", message);

    public static void Warn(string message) => WriteLine("WARN", message);

    public static void Error(string message) => WriteLine("ERROR", message);

    public static void Exception(string context, Exception exception)
    {
        WriteLine("ERROR", context);
        WriteLine("ERROR", exception.ToString());
    }

    private static void WriteLine(string level, string message)
    {
        lock (SyncRoot)
        {
            if (_writer is null)
            {
                return;
            }

            _writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}");
        }
    }
}
