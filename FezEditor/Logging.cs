using System.Globalization;
using FezEditor.Services;
using Microsoft.Xna.Framework;
using Serilog;
using Serilog.Events;

namespace FezEditor;

public static class Logging
{
    private const string LogTemplate =
        "({Timestamp:HH:mm:ss.fff}) {Level:u4} [{SourceContext}] {Message:lj}{NewLine}{Exception}";

#if DEBUG
    public static LogEventLevel Level { get; set; } = LogEventLevel.Debug;
#else
    public static LogEventLevel Level { get; set; } = LogEventLevel.Information;
#endif

    public static void Initialize()
    {
        var logFile = Path.Combine(AppStorageService.BaseDir, "Logs",
            $"[{DateTime.Now:yyyy-MM-ddTHH-mm-ss}] {Level} Log.txt");

        CleanOldLogFiles(logFile);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(Level)
            .WriteTo.Console(outputTemplate: LogTemplate)
            .WriteTo.File(logFile, outputTemplate: LogTemplate)
            .CreateLogger();

        var logger = Log.ForContext("SourceContext", "FNA");
        FNALoggerEXT.LogInfo = msg => logger.Information("{Message}", msg);
        FNALoggerEXT.LogWarn = msg => logger.Warning("{Message}", msg);
        FNALoggerEXT.LogError = msg => logger.Error("{Message}", msg);
    }

    private static void CleanOldLogFiles(string logFile)
    {
        var directory = Path.GetDirectoryName(logFile)!;
        if (Directory.Exists(directory))
        {
            var cutoff = DateTime.Now.AddDays(-3);
            foreach (var file in Directory.GetFiles(directory, "*.txt"))
            {
                if (TryParseLogFileDate(file, out var fileDate) && fileDate < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
    }

    private static bool TryParseLogFileDate(string file, out DateTime date)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        return DateTime.TryParseExact(name[1..20], "yyyy-MM-ddTHH-mm-ss",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    public static ILogger Create<T>()
    {
        return Log.ForContext("SourceContext", typeof(T).Name);
    }
}