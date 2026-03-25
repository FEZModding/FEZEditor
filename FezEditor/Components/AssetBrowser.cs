using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Serilog;

namespace FezEditor.Components;

public class AssetBrowser : IDisposable
{
    private static readonly ILogger Logger = Logging.Create<AssetBrowser>();

    private static readonly Dictionary<string, Texture2D> SharedThumbnails = new();

    private static int s_instanceCount;

    private const float ThumbSize = 64f;

    private const float CellSpacing = 8f;

    private const float CellSize = ThumbSize + CellSpacing;

    private const float LabelHeight = 20f;

    private const float RowHeight = CellSize + LabelHeight;

    private const int MaxThumbnailsPerFrame = 2;

    private readonly string _title;

    private readonly ResourceService _resources;

    private readonly Dictionary<Kind, IReadOnlyList<Entry>> _entries = new();

    private readonly Queue<Entry> _pendingQueue = new();

    private string? _trileSetPath;

    private TrileSet? _trileSet;

    private readonly Dictionary<CollisionType, RTexture2D> _collisionTextures = new();

    private Texture2D _placeholder = null!;

    private Entry? _selectedEntry;

    private string _filterEntries = string.Empty;

    public AssetBrowser(Game game, string title)
    {
        _title = title;
        _resources = game.GetService<ResourceService>();
        _resources.ProviderChanged += OnProviderChanged;
        s_instanceCount++;
    }

    public void SetTrileSet(string path, TrileSet set)
    {
        _trileSetPath = path;
        _trileSet = set;
        _entries.Clear();
    }

    public void LoadContent(IContentManager content)
    {
        _placeholder = content.Load<Texture2D>("Missing");
        foreach (var collision in Enum.GetValues<CollisionType>())
        {
            var texture = content.Load<Texture2D>($"Textures/{collision}");
            var data = new byte[texture.Width * texture.Height * 4];
            texture.GetData(data);
            _collisionTextures[collision] = new RTexture2D
            {
                Width = texture.Width,
                Height = texture.Height,
                TextureData = data
            };
        }
    }

    public bool Select(out Entry? entry)
    {
        if (_selectedEntry.HasValue)
        {
            entry = _selectedEntry.Value;
            return true;
        }

        entry = null;
        return false;
    }

    public void Unselect()
    {
        _selectedEntry = null;
    }

    public void Draw(ref bool isOpen)
    {
        #region Process Queue

        var generated = 0;
        while (_pendingQueue.Count > 0)
        {
            var entry = _pendingQueue.Dequeue();

            try
            {
                var lastWrite = _resources.GetLastWriteTimeUtc(entry.Path);

                // Get thumbnail from cache
                var cacheProbe = new ThumbnailGenerator(entry.CachePath, lastWrite);
                var cached = cacheProbe.Load();
                if (cached != null)
                {
                    SharedThumbnails[entry.CachePath] = RepackerExtensions.ConvertToTexture2D(cached);
                    continue;
                }

                // Cache miss - limit expensive generation to avoid stalling
                if (generated >= MaxThumbnailsPerFrame)
                {
                    _pendingQueue.Enqueue(entry);
                    break;
                }

                // Load asset and generate thumbnail
                ThumbnailGenerator? generator = null;
                switch (entry.Kind)
                {
                    case Kind.ArtObjects:
                        {
                            var asset = _resources.Load(entry.Path);
                            if (asset is ArtObject ao)  // exclude AOs with texture only
                            {
                                generator = new ThumbnailGenerator(entry.CachePath, lastWrite, ao);
                            }

                            break;
                        }

                    case Kind.Triles:
                        {
                            if (_trileSet != null)
                            {
                                var trile = _trileSet.FindByName(entry.Name);
                                if (trile.Geometry.Vertices.Length > 0)
                                {
                                    var atlas = _trileSet.TextureAtlas;
                                    generator = new ThumbnailGenerator(entry.CachePath, lastWrite, trile, atlas);
                                }
                                else if (trile.Faces.TryGetValue(FaceOrientation.Front, out var collisionType) &&
                                         _collisionTextures.TryGetValue(collisionType, out var collisionTex))
                                {
                                    generator = new ThumbnailGenerator(entry.CachePath, lastWrite, collisionTex);
                                }
                            }

                            break;
                        }

                    case Kind.BackgroundPlanes:
                        {
                            var asset = _resources.Load(entry.Path);
                            if (asset is RAnimatedTexture anim)
                            {
                                generator = new ThumbnailGenerator(entry.CachePath, lastWrite, anim);
                            }
                            else if (asset is RTexture2D tex)
                            {
                                generator = new ThumbnailGenerator(entry.CachePath, lastWrite, tex);
                            }

                            break;
                        }

                    case Kind.NonPlayableCharacters:
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
                                generator = new ThumbnailGenerator(entry.CachePath, lastWrite, selected);
                            }

                            break;
                        }

                    default:
                        throw new InvalidOperationException();
                }

                if (generator == null)
                {
                    continue;
                }

                var thumb = generator.Generate();
                SharedThumbnails[entry.CachePath] = RepackerExtensions.ConvertToTexture2D(thumb);
                generator.Save(thumb);
                generated++;
            }
            catch (Exception e)
            {
                Logger.Warning(e, "Failed to generate thumbnail for {0}", entry.Path);
            }
        }

        #endregion

        if (!isOpen)
        {
            return;
        }

        TryBuildEntries();

        if (ImGui.Begin($"Asset Browser##{_title}", ref isOpen, ImGuiWindowFlags.NoCollapse))
        {
            var isGenerating = _pendingQueue.Count > 0;
            if (isGenerating)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button($"{Icons.Refresh} Refresh Thumbnails"))
            {
                foreach (var entry in _entries.Values.SelectMany(e => e))
                {
                    new ThumbnailGenerator(entry.CachePath, default).Delete();
                }

                ClearSharedThumbnails(_placeholder);
                _entries.Clear();
            }

            if (isGenerating)
            {
                ImGui.EndDisabled();
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(256);
            ImGui.InputTextWithHint("", "Filter assets...", ref _filterEntries, 255);

            if (!string.IsNullOrEmpty(_filterEntries))
            {
                ImGui.SameLine();
                if (ImGui.Button($"{Icons.Close}"))
                {
                    _filterEntries = string.Empty;
                }
            }

            if (_pendingQueue.Count > 0)
            {
                var spinner = "|/-\\"[(int)(ImGui.GetTime() * 8) % 4];
                ImGui.SameLine();
                ImGui.TextDisabled($"{spinner} Generating thumbnails... ({_pendingQueue.Count} remaining)");
            }

            if (_entries.Count > 0 && ImGui.BeginTabBar("##AssetTabs"))
            {
                foreach (var type in Enum.GetValues<Kind>())
                {
                    var label = type switch
                    {
                        Kind.Triles => "Triles",
                        Kind.ArtObjects => "Art Objects",
                        Kind.BackgroundPlanes => "Planes",
                        Kind.NonPlayableCharacters => "NPCs/Critters",
                        _ => throw new InvalidOperationException()
                    };

                    if (ImGui.BeginTabItem(label))
                    {
                        if (type == Kind.Triles && _trileSetPath != null)
                        {
                            ImGui.TextDisabled(_trileSetPath);
                            ImGui.Separator();
                        }

                        if (_entries.TryGetValue(type, out var entries))
                        {
                            IReadOnlyList<Entry> ret;
                            if (string.IsNullOrEmpty(_filterEntries))
                            {
                                ret = entries;
                            }
                            else
                            {
                                ret = entries
                                    .Where(entry => entry.Name.Contains(_filterEntries, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                            }

                            DrawGrid(ret);
                        }

                        ImGui.EndTabItem();
                    }
                }

                ImGui.EndTabBar();
            }

            ImGui.End();
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _resources.ProviderChanged -= OnProviderChanged;

        s_instanceCount--;
        if (s_instanceCount <= 0)
        {
            ClearSharedThumbnails(_placeholder);
        }
    }

    private void TryBuildEntries()
    {
        if (_entries.Count > 0 || _resources.HasNoProvider)
        {
            return;
        }

        var triles = new List<Entry>();
        var artObjects = new List<Entry>();
        var planes = new List<Entry>();
        var npcFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var npcs = new List<Entry>();

        if (_trileSet != null && _trileSetPath != null)
        {
            triles.AddRange(_trileSet.Triles.Values.Select(trile => new Entry(trile.Name, _trileSetPath, Kind.Triles)));
        }

        foreach (var file in _resources.Files)
        {
            if (file.StartsWith("Art Objects/", StringComparison.OrdinalIgnoreCase))
            {
                artObjects.Add(new Entry(file["Art Objects/".Length..], file, Kind.ArtObjects));
            }
            else if (file.StartsWith("Background Planes/", StringComparison.OrdinalIgnoreCase))
            {
                planes.Add(new Entry(file["Background Planes/".Length..], file, Kind.BackgroundPlanes));
            }
            else if (file.StartsWith("Character Animations/", StringComparison.OrdinalIgnoreCase) &&
                     !file.Contains("Metadata", StringComparison.OrdinalIgnoreCase))
            {
                var remainder = file["Character Animations/".Length..];
                var slashIndex = remainder.IndexOf('/');
                if (slashIndex >= 0)
                {
                    var folder = remainder[..slashIndex];
                    if (npcFolders.Add(folder))
                    {
                        npcs.Add(new Entry(folder, $"Character Animations/{folder}", Kind.NonPlayableCharacters));
                    }
                }
            }
        }

        artObjects.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        triles.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        planes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        npcs.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        _entries[Kind.ArtObjects] = artObjects;
        _entries[Kind.BackgroundPlanes] = planes;
        _entries[Kind.NonPlayableCharacters] = npcs;
        _entries[Kind.Triles] = triles;

        // Pre-fill thumbnails with placeholder and enqueue for generation
        foreach (var entry in _entries.Values.SelectMany(e => e))
        {
            if (SharedThumbnails.TryAdd(entry.CachePath, _placeholder) ||
                SharedThumbnails[entry.CachePath] == _placeholder)
            {
                _pendingQueue.Enqueue(entry);
            }
        }

        Logger.Debug("Built {0} art objects, {1} triles, {2} planes, {3} NPCs",
            artObjects.Count, triles.Count, planes.Count, npcs.Count);
    }

    private unsafe void DrawGrid(IReadOnlyList<Entry> entries)
    {
        var availWidth = ImGui.GetContentRegionAvail().X;
        var columns = Math.Max((int)(availWidth / CellSize), 1);
        var totalRows = (entries.Count + columns - 1) / columns;

        if (!ImGui.BeginTable("##grid", columns, ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchSame))
        {
            return;
        }

        var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
        clipper.Begin(totalRows, RowHeight);

        while (clipper.Step())
        {
            for (var row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, RowHeight);

                for (var col = 0; col < columns; col++)
                {
                    var i = row * columns + col;
                    if (i >= entries.Count)
                    {
                        break;
                    }

                    ImGui.TableSetColumnIndex(col);

                    var entry = entries[i];
                    var isSelected = _selectedEntry == entry;
                    var texture = SharedThumbnails.GetValueOrDefault(entry.CachePath, _placeholder);
                    var cellWidth = ImGui.GetColumnWidth();

                    ImGui.PushID(i);

                    // Compute thumbnail size preserving aspect ratio
                    var aspect = (float)texture.Width / texture.Height;
                    float thumbW, thumbH;
                    if (aspect >= 1f)
                    {
                        thumbW = ThumbSize;
                        thumbH = ThumbSize / aspect;
                    }
                    else
                    {
                        thumbH = ThumbSize;
                        thumbW = ThumbSize * aspect;
                    }

                    // Center thumbnail within the cell
                    var padX = (cellWidth - thumbW) * 0.5f;
                    var padY = (ThumbSize - thumbH) * 0.5f;
                    var cursor = ImGui.GetCursorPos();
                    var cellScreenPos = ImGui.GetCursorScreenPos();
                    ImGui.SetCursorPos(new NVector2(cursor.X + padX, cursor.Y + padY));
                    ImGuiX.Image(texture, new Vector2(thumbW, thumbH));

                    // Highlight selected asset on top of thumbnail
                    if (isSelected)
                    {
                        var dl = ImGui.GetWindowDrawList();
                        var highlightMax = new NVector2(cellScreenPos.X + ThumbSize, cellScreenPos.Y + ThumbSize);
                        var color = Color.LightGray with { A = 128 }; // 50%
                        dl.AddRectFilled(cellScreenPos, highlightMax, color.PackedValue);
                    }

                    // Restore cursor for the invisible click target over the whole cell
                    ImGui.SetCursorPos(cursor);
                    if (ImGui.InvisibleButton("##sel", new NVector2(cellWidth, ThumbSize)))
                    {
                        _selectedEntry = entry;
                    }

                    // Label wrapped and centered below thumbnail
                    ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + cellWidth);
                    var textSize = ImGui.CalcTextSize(entry.Name, true);
                    var labelPad = (cellWidth - textSize.X) * 0.5f;
                    if (labelPad > 0)
                    {
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + labelPad);
                    }

                    ImGui.TextUnformatted(entry.Name);
                    ImGui.PopTextWrapPos();

                    ImGui.PopID();
                }
            }
        }

        clipper.End();
        clipper.Destroy();
        ImGui.EndTable();
    }

    private void OnProviderChanged()
    {
        _entries.Clear();
        _pendingQueue.Clear();
        _selectedEntry = null;
    }

    private static void ClearSharedThumbnails(Texture2D? placeholder = null)
    {
        foreach (var tex in SharedThumbnails.Values)
        {
            if (tex != placeholder)
            {
                tex.Dispose();
            }
        }

        SharedThumbnails.Clear();
    }

    public readonly record struct Entry(string Name, string Path, Kind Kind)
    {
        public string CachePath => Kind == Kind.Triles ? $"{Path}/{Name}" : Path;
    }

    public enum Kind
    {
        Triles,
        ArtObjects,
        BackgroundPlanes,
        NonPlayableCharacters
    }
}