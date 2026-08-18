using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Serilog;

namespace FezEditor.Components;

[UsedImplicitly]
public class ThumbnailGenerator : DrawableGameComponent
{
    private static readonly ILogger Logger = Log.ForContext<ThumbnailGenerator>();

    private readonly ResourceService _resources;

    private readonly StatusService _statusService;

    private static readonly Dictionary<CollisionType, RTexture2D> CollisionTextures = new();

    private CancellationTokenSource? _cts;

    private int _complete;

    private bool _disposed;

    public ThumbnailGenerator(Game game) : base(game)
    {
        _resources = game.GetService<ResourceService>();
        _statusService = game.GetService<StatusService>();
    }

    protected override void LoadContent()
    {
        if (CollisionTextures.Count == 0)
        {
            var content = Game.GetService<ContentService>().Global;
            foreach (var collision in Enum.GetValues<CollisionType>())
            {
                var texture = content.Load<Texture2D>($"Textures/{collision}");
                var data = new byte[texture.Width * texture.Height * 4];
                texture.GetData(data);
                CollisionTextures[collision] = new RTexture2D
                {
                    Width = texture.Width,
                    Height = texture.Height,
                    TextureData = data
                };
            }
        }

        _ = ProcessAsync();
    }

    public override void Update(GameTime gameTime)
    {
        if (Volatile.Read(ref _complete) != 0)
        {
            Game.RemoveComponent(this);
        }
    }

    public void Cancel()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The worker completed between reading and cancelling the source.
        }
    }

    private async Task ProcessAsync()
    {
        var cts = new CancellationTokenSource();
        _cts = cts;

        try
        {
            await Task.Run(() => ProcessInternal(cts.Token), cts.Token);
        }
        catch (OperationCanceledException)
        {
            Logger.Debug("Thumbnail generation cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Thumbnail generation failed");
        }
        finally
        {
            _cts = null;
            cts.Dispose();
            Volatile.Write(ref _complete, 1);
        }
    }

    private void ProcessInternal(CancellationToken ct)
    {
        var providerRoot = _resources.RootPath;
        var previousSources = ThumbnailDatabase.GetProviderSources(providerRoot);
        var sources = new Dictionary<string, ThumbnailDatabase.SourceRecord>(StringComparer.OrdinalIgnoreCase);
        var entries = new Queue<Entry>();
        var pending = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var npcFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = _resources.Files.ToArray();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var extension = string.Empty;
            try
            {
                extension = _resources.GetExtension(file);
                if (file.StartsWith("Trile Sets/", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".fezts.glb", StringComparison.OrdinalIgnoreCase))
                {
                    var lastWrite = _resources.GetLastWriteTimeUtc(file);
                    var sourceKey = GetSourceKey(file, AssetType.Trile);
                    List<string> thumbnailPaths;
                    if (previousSources.TryGetValue(sourceKey, out var previous) &&
                        previous.LastWrite == lastWrite)
                    {
                        thumbnailPaths = previous.ThumbnailPaths;
                    }
                    else
                    {
                        var trileNames = _resources.GetTrileSetList(file);
                        thumbnailPaths = trileNames.Values
                            .Select(name => new Entry(file, AssetType.Trile, lastWrite, sourceKey, name).CachePath)
                            .ToList();
                    }

                    AddSource(previousSources, sources, entries, pending, sourceKey, file,
                        AssetType.Trile, lastWrite, thumbnailPaths);
                }
                else if (file.StartsWith("Art Objects/", StringComparison.OrdinalIgnoreCase) ||
                         extension.Equals(".fezao.glb", StringComparison.OrdinalIgnoreCase))
                {
                    if (!extension.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        AddSingleSource(previousSources, sources, entries, pending, file,
                            AssetType.ArtObject, _resources.GetLastWriteTimeUtc(file));
                    }
                }
                else if (file.StartsWith("Background Planes/", StringComparison.OrdinalIgnoreCase))
                {
                    AddSingleSource(previousSources, sources, entries, pending, file,
                        AssetType.BackgroundPlane, _resources.GetLastWriteTimeUtc(file));
                }
                else if (file.StartsWith("Character Animations/", StringComparison.OrdinalIgnoreCase) &&
                         !file.Contains("Metadata", StringComparison.OrdinalIgnoreCase))
                {
                    var remainder = file["Character Animations/".Length..];
                    var slashIndex = remainder.IndexOf('/');
                    if (slashIndex >= 0)
                    {
                        var folder = $"Character Animations/{remainder[..slashIndex]}";
                        if (npcFolders.Add(folder))
                        {
                            var prefix = folder + "/";
                            var lastWrite = files
                                .Where(candidate => candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                .Select(_resources.GetLastWriteTimeUtc)
                                .DefaultIfEmpty(DateTime.MinValue)
                                .Max();
                            AddSingleSource(previousSources, sources, entries, pending, folder,
                                AssetType.NonPlayableCharacter, lastWrite);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to inspect thumbnail source {0}", file);
                var type = GetAssetType(file, extension);
                if (type.HasValue)
                {
                    var sourceKey = GetSourceKey(file, type.Value);
                    sources[sourceKey] = new ThumbnailDatabase.SourceRecord
                    {
                        LastWrite = _resources.GetLastWriteTimeUtc(file),
                        Complete = true,
                        Failed = true
                    };
                }
            }
        }

        var processed = 0;
        var total = entries.Count;
        if (total == 0)
        {
            ThumbnailDatabase.SetProviderSources(providerRoot, sources);
            ThumbnailDatabase.Flush();
            return;
        }

        using var activity = _statusService.BeginActivity($"Generating thumbnails (0/{total})", 0f);
        TrileSet? cachedTrileSet = null;
        string? cachedTrileSetPath = null;

        try
        {
            while (entries.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var entry = entries.Dequeue();
                try
                {
                    var lastWrite = entry.LastWrite;
                    var cachePath = entry.CachePath;

                    var cacheProbe = new Thumbnailer(cachePath, lastWrite);
                    if (cacheProbe.IsCacheCurrent())
                    {
                        Logger.Debug("Thumbnail for {0} already cached", cachePath);
                        continue;
                    }

                    Thumbnailer? thumbnailer = null;
                    switch (entry.Type)
                    {
                    case AssetType.ArtObject:
                        {
                            var ao = _resources.Load<ArtObject>(entry.Path);
                            thumbnailer = new Thumbnailer(cachePath, lastWrite, ao);
                            break;
                        }

                    case AssetType.Trile:
                        {
                            if (cachedTrileSetPath != entry.Path)
                            {
                                cachedTrileSet = _resources.Load<TrileSet>(entry.Path);
                                cachedTrileSetPath = entry.Path;
                            }

                            var trile = cachedTrileSet!.Triles.Values
                                .FirstOrDefault(t => t.Name == entry.TrileName);
                            if (trile == null)
                            {
                                break;
                            }

                            if (!trile.Geometry.IsNullOrEmpty())
                            {
                                thumbnailer = new Thumbnailer(cachePath, lastWrite, trile, cachedTrileSet.TextureAtlas);
                            }
                            else if (trile.Faces.TryGetValue(FaceOrientation.Front, out var collisionType) &&
                                     CollisionTextures.TryGetValue(collisionType, out var collisionTex))
                            {
                                thumbnailer = new Thumbnailer(cachePath, lastWrite, collisionTex);
                            }

                            break;
                        }

                    case AssetType.BackgroundPlane:
                        {
                            var asset = _resources.Load<object>(entry.Path);
                            if (asset is RAnimatedTexture anim)
                            {
                                thumbnailer = new Thumbnailer(cachePath, lastWrite, anim);
                            }
                            else if (asset is RTexture2D tex)
                            {
                                thumbnailer = new Thumbnailer(cachePath, lastWrite, tex);
                            }

                            break;
                        }

                    case AssetType.NonPlayableCharacter:
                        {
                            var animations = _resources.LoadAnimations(entry.Path);

                            RAnimatedTexture? selected = null;
                            if (animations.TryGetValue("IdleWink", out var idleWink))
                            {
                                selected = idleWink;
                            }
                            else if (animations.TryGetValue("Idle", out var idle))
                            {
                                selected = idle;
                            }
                            else if (animations.TryGetValue("Walk", out var walk))
                            {
                                selected = walk;
                            }
                            else if (animations.Count > 0)
                            {
                                selected = animations.Values.First();
                            }

                            if (selected != null)
                            {
                                thumbnailer = new Thumbnailer(cachePath, lastWrite, selected);
                            }

                            break;
                        }

                    default:
                        throw new InvalidOperationException();
                    }

                    if (thumbnailer != null)
                    {
                        var thumbnail = thumbnailer.Generate();
                        thumbnailer.Save(thumbnail);
                    }
                    else
                    {
                        sources[entry.SourceKey].Failed = true;
                    }
                }
                catch (Exception e)
                {
                    Logger.Warning(e, "Failed to generate thumbnail for {0}", entry.CachePath);
                    sources[entry.SourceKey].Failed = true;
                }
                finally
                {
                    if (--pending[entry.SourceKey] == 0)
                    {
                        sources[entry.SourceKey].Complete = true;
                    }

                    processed++;
                    ReportProgress(activity, processed, total);
                }
            }
        }
        finally
        {
            ThumbnailDatabase.SetProviderSources(providerRoot, sources);
            ThumbnailDatabase.Flush();
        }
    }

    private static void ReportProgress(StatusActivityHandle activity, int processed, int total)
    {
        var progress = total == 0 ? 1f : (float)processed / total;
        activity.Report($"Generating thumbnails ({processed}/{total})", progress);
    }

    private static void AddSingleSource(
        Dictionary<string, ThumbnailDatabase.SourceRecord> previousSources,
        Dictionary<string, ThumbnailDatabase.SourceRecord> sources,
        Queue<Entry> entries,
        Dictionary<string, int> pending,
        string path,
        AssetType type,
        DateTime lastWrite)
    {
        var sourceKey = GetSourceKey(path, type);
        var thumbnailPath = new Entry(path, type, lastWrite, sourceKey).CachePath;
        AddSource(previousSources, sources, entries, pending, sourceKey, path, type, lastWrite, [thumbnailPath]);
    }

    private static void AddSource(
        Dictionary<string, ThumbnailDatabase.SourceRecord> previousSources,
        Dictionary<string, ThumbnailDatabase.SourceRecord> sources,
        Queue<Entry> entries,
        Dictionary<string, int> pending,
        string sourceKey,
        string path,
        AssetType type,
        DateTime lastWrite,
        List<string> thumbnailPaths)
    {
        var record = new ThumbnailDatabase.SourceRecord
        {
            LastWrite = lastWrite,
            ThumbnailPaths = thumbnailPaths,
            Complete = false
        };
        sources[sourceKey] = record;

        var unchanged = previousSources.TryGetValue(sourceKey, out var previous) &&
                        previous.LastWrite == lastWrite && previous.Complete;
        if (unchanged && previous!.Failed)
        {
            record.Complete = true;
            record.Failed = true;
            return;
        }

        var stalePaths = thumbnailPaths
            .Where(thumbnailPath => !unchanged || !new Thumbnailer(thumbnailPath, lastWrite).IsCacheCurrent())
            .ToList();

        if (stalePaths.Count == 0)
        {
            record.Complete = true;
            return;
        }

        pending[sourceKey] = stalePaths.Count;
        var basePath = new Entry(path, type, lastWrite, sourceKey).CachePath;
        foreach (var thumbnailPath in stalePaths)
        {
            var trileName = type == AssetType.Trile ? thumbnailPath[(basePath.Length + 1)..] : null;
            entries.Enqueue(new Entry(path, type, lastWrite, sourceKey, trileName));
        }
    }

    private static string GetSourceKey(string path, AssetType type)
    {
        return $"{type}:{path}".Replace('\\', '/').ToLowerInvariant();
    }

    private static AssetType? GetAssetType(string path, string extension)
    {
        if (path.StartsWith("Trile Sets/", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".fezts.glb", StringComparison.OrdinalIgnoreCase))
        {
            return AssetType.Trile;
        }

        if (path.StartsWith("Art Objects/", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".fezao.glb", StringComparison.OrdinalIgnoreCase))
        {
            return AssetType.ArtObject;
        }

        if (path.StartsWith("Background Planes/", StringComparison.OrdinalIgnoreCase))
        {
            return AssetType.BackgroundPlane;
        }

        return null;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
            Cancel();
            base.Dispose(disposing);
        }
    }

    private readonly record struct Entry(
        string Path,
        AssetType Type,
        DateTime LastWrite,
        string SourceKey,
        string? TrileName = null)
    {
        public string CachePath
        {
            get
            {
                var prefix = Type switch
                {
                    AssetType.Trile => "Trile Sets/",
                    AssetType.ArtObject => "Art Objects/",
                    AssetType.BackgroundPlane => "Background Planes/",
                    AssetType.NonPlayableCharacter => "Character Animations/",
                    _ => throw new ArgumentOutOfRangeException()
                };

                var path = Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? Path
                    : prefix + Path;

                return TrileName != null ? $"{path}/{TrileName}" : path;
            }
        }
    }
}