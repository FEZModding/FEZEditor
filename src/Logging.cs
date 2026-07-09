using System.Globalization;
using FezEditor.Services;
using Microsoft.Xna.Framework;
using Serilog;
using Serilog.Events;
using Serilog.Templates;
using Serilog.Templates.Themes;

namespace FezEditor;

public static class Logging
{
    private const string LogTemplate =
        "({UtcDateTime(@t):HH:mm:ss.fff}) {@l:u4} [{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}] {@m}\n{@x}";

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
            .WriteTo.Console(formatter: new ExpressionTemplate(LogTemplate, theme: TemplateTheme.Literate))
            .WriteTo.File(formatter: new ExpressionTemplate(LogTemplate), path: logFile)
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
}