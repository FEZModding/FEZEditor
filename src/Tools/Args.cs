using System.Globalization;
using Serilog.Events;

namespace FezEditor.Tools;

public static class Args
{
    public static void Parse(string[] args)
    {
        var queue = new Queue<string>(args);
        while (queue.Count > 0)
        {
            switch (queue.Dequeue().ToLower(CultureInfo.InvariantCulture))
            {
                case "--log-level":
                    if (queue.Count > 0 && Enum.TryParse<LogEventLevel>(queue.Dequeue(), true, out var level))
                    {
                        Logging.Level = level;
                    }

                    break;
            }
        }
    }
}