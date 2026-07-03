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
    private static readonly ILogger Logger = Logging.Create<ThumbnailGenerator>();

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
        using var activity = _statusService.BeginActivity("Scanning thumbnails...");

        try
        {
            await Task.Run(() => ProcessInternal(cts.Token, activity), cts.Token);
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

    private void ProcessInternal(CancellationToken ct, StatusActivityHandle activity)
    {
        var entries = new Queue<Entry>();
        var npcFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in _resources.Files.ToArray())
        {
            ct.ThrowIfCancellationRequested();
            var extension = _resources.GetExtension(file);
            if (file.StartsWith("Trile Sets/", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".fezts.glb", StringComparison.OrdinalIgnoreCase))
            {
                var lastWrite = _resources.GetLastWriteTimeUtc(file);
                var trileNames = _resources.GetTrileSetList(file);
                foreach (var name in trileNames.Values)
                {
                    var entry = new Entry(file, AssetType.Trile, name);
                    if (!new Thumbnailer(entry.CachePath, lastWrite).IsCacheCurrent())
                    {
                        entries.Enqueue(entry);
                    }
                }
            }
            else if (file.StartsWith("Art Objects/", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".fezao.glb", StringComparison.OrdinalIgnoreCase))
            {
                if (!extension.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    EnqueueIfStale(entries, new Entry(file, AssetType.ArtObject));
                }
            }
            else if (file.StartsWith("Background Planes/", StringComparison.OrdinalIgnoreCase))
            {
                EnqueueIfStale(entries, new Entry(file, AssetType.BackgroundPlane));
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
                        EnqueueIfStale(entries, new Entry(folder, AssetType.NonPlayableCharacter));
                    }
                }
            }
        }

        var processed = 0;
        var total = entries.Count;
        activity.Report(total == 0 ? "Thumbnails are up to date" : $"Generating thumbnails (0/{total})", 0f);
        TrileSet? cachedTrileSet = null;
        string? cachedTrileSetPath = null;

        while (entries.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var entry = entries.Dequeue();
            try
            {
                var lastWrite = GetLastWriteTimeUtc(entry);
                var cachePath = entry.CachePath;

                var cacheProbe = new Thumbnailer(cachePath, lastWrite);
                if (cacheProbe.IsCacheCurrent())
                {
                    Logger.Debug("Thumbnail for {0} already cached", cachePath);
                    processed++;
                    ReportProgress(activity, processed, total);
                    continue;
                }

                Thumbnailer thumbnailer = null!;
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

                            if (trile.Geometry.Indices.Length > 0)
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

                processed++;
                ReportProgress(activity, processed, total);
            }
            catch (Exception e)
            {
                Logger.Warning(e, "Failed to generate thumbnail for {0}", entry.CachePath);
                processed++;
                ReportProgress(activity, processed, total);
            }
        }
    }

    private static void ReportProgress(StatusActivityHandle activity, int processed, int total)
    {
        var progress = total == 0 ? 1f : (float)processed / total;
        activity.Report($"Generating thumbnails ({processed}/{total})", progress);
    }

    private void EnqueueIfStale(Queue<Entry> entries, Entry entry)
    {
        var lastWrite = GetLastWriteTimeUtc(entry);
        if (!new Thumbnailer(entry.CachePath, lastWrite).IsCacheCurrent())
        {
            entries.Enqueue(entry);
        }
    }

    private DateTime GetLastWriteTimeUtc(Entry entry)
    {
        if (entry.Type != AssetType.NonPlayableCharacter)
        {
            return _resources.GetLastWriteTimeUtc(entry.Path);
        }

        var prefix = entry.Path + "/";
        return _resources.Files
            .Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(_resources.GetLastWriteTimeUtc)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();
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

    private readonly record struct Entry(string Path, AssetType Type, string? TrileName = null)
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