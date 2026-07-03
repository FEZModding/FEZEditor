using FezEditor.Services;

namespace FezEditor.Tools;

internal static class ThumbnailDatabase
{
    private const string Filename = "ThumbnailDatabase.json";

    private const int CurrentVersion = 1;

    private static readonly Lock Sync = new();

    private static Database? s_database;

    private static bool s_dirty;

    public static bool IsThumbnailCurrent(string path, DateTime lastWrite)
    {
        lock (Sync)
        {
            return GetDatabase().Thumbnails.TryGetValue(Normalize(path), out var thumbnail) &&
                   thumbnail.LastWrite == lastWrite;
        }
    }

    public static void SetThumbnailCurrent(string path, DateTime lastWrite)
    {
        lock (Sync)
        {
            GetDatabase().Thumbnails[Normalize(path)] = new ThumbnailRecord { LastWrite = lastWrite };
            s_dirty = true;
        }
    }

    public static Dictionary<string, SourceRecord> GetProviderSources(string rootPath)
    {
        lock (Sync)
        {
            if (!GetDatabase().Providers.TryGetValue(Normalize(rootPath), out var provider))
            {
                return new Dictionary<string, SourceRecord>(StringComparer.OrdinalIgnoreCase);
            }

            return provider.Sources.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Clone(),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void SetProviderSources(string rootPath, Dictionary<string, SourceRecord> sources)
    {
        lock (Sync)
        {
            GetDatabase().Providers[Normalize(rootPath)] = new ProviderRecord
            {
                Sources = sources.ToDictionary(
                    pair => Normalize(pair.Key),
                    pair => pair.Value.Clone(),
                    StringComparer.OrdinalIgnoreCase)
            };
            s_dirty = true;
        }
    }

    public static void Flush()
    {
        lock (Sync)
        {
            if (!s_dirty)
            {
                return;
            }

            if (AppStorageService.SaveCacheJson(Filename, GetDatabase()))
            {
                s_dirty = false;
            }
        }
    }

    public static void Reset()
    {
        lock (Sync)
        {
            s_database = new Database();
            s_dirty = false;
        }
    }

    private static Database GetDatabase()
    {
        if (s_database != null)
        {
            return s_database;
        }

        if (!AppStorageService.TryLoadCacheJson<Database>(Filename, out var database) ||
            database!.Version != CurrentVersion || database.Thumbnails == null || database.Providers == null)
        {
            database = new Database();
        }

        s_database = database;
        return database;
    }

    private static string Normalize(string value)
    {
        return value.Replace('\\', '/').ToLowerInvariant();
    }

    internal sealed class SourceRecord
    {
        public DateTime LastWrite { get; set; }

        public bool Complete { get; set; }

        public bool Failed { get; set; }

        public List<string> ThumbnailPaths { get; set; } = new();

        public SourceRecord Clone()
        {
            return new SourceRecord
            {
                LastWrite = LastWrite,
                Complete = Complete,
                Failed = Failed,
                ThumbnailPaths = new List<string>(ThumbnailPaths)
            };
        }
    }

    private sealed class Database
    {
        public int Version { get; set; } = CurrentVersion;

        public Dictionary<string, ThumbnailRecord> Thumbnails { get; set; } = new();

        public Dictionary<string, ProviderRecord> Providers { get; set; } = new();
    }

    private sealed class ThumbnailRecord
    {
        public DateTime LastWrite { get; set; }
    }

    private sealed class ProviderRecord
    {
        public Dictionary<string, SourceRecord> Sources { get; set; } = new();
    }
}