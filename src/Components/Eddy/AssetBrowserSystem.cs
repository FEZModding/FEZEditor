using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Eddy;

public class AssetBrowserSystem : ThumbnailBrowserSystem<AssetEntry>
{
    private const float SelectionThumbSize = 64f;

    private const float RecentThumbSize = 48f;

    private const float TileSpacing = 10f;

    private readonly HashSet<AssetEntry> _entries = new();

    public override void Initialize()
    {
        Resources.ProviderChanged += OnProviderChanged;
        Resources.ThumbnailsReady += LoadAssetEntries;
        LoadAssetEntries();
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        Resources.ProviderChanged -= OnProviderChanged;
        Resources.ThumbnailsReady -= LoadAssetEntries;
    }

    public override void Draw()
    {
        if (!Eddy.ShowAssetBrowser)
        {
            return;
        }

        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoCollapse;
        ImGuiX.SetNextWindowSize(new Vector2(700, 500), ImGuiCond.FirstUseEver);

        var isOpen = Eddy.ShowAssetBrowser;
        if (ImGui.Begin("Asset Browser", ref isOpen, flags))
        {
            DrawSelectionBar();
            ImGui.Separator();

            DrawBrowserToolbar("Filter assets...");
            ImGui.Separator();

            if (ImGui.BeginChild("##Content"))
            {
                if (_entries.Count > 0 && ImGui.BeginTabBar("##AssetTabs"))
                {
                    var groups = _entries.GroupBy(ae => ae.GetType());

                    foreach (var group in groups)
                    {
                        if (ImGui.BeginTabItem(GetLabel(group.Key)))
                        {
                            if (group.Key == typeof(AssetEntry.Trile))
                            {
                                ImGui.TextDisabled(Eddy.TrileSet.Name);
                                ImGui.Separator();
                            }

                            var entries = group
                                .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                                .ToList();

                            if (!string.IsNullOrEmpty(FilterText))
                            {
                                entries = entries.FindAll(ae =>
                                    MatchesFilter(ae.DisplayName));
                            }

                            DrawTileGrid("##AssetGrid", entries, scrollToSelection: true);

                            ImGui.EndTabItem();
                        }
                    }

                    ImGui.EndTabBar();
                }

            }

            ImGui.EndChild();
            ImGui.End();
        }

        if (!isOpen)
        {
            Eddy.ShowAssetBrowser = false;
        }
    }

    private void DrawSelectionBar()
    {
        // Show the most recently selected entry across all types
        var selected = Eddy.RecentEntries.Count > 0 ? Eddy.RecentEntries[0] : null;

        // Header line: "Selected: Name (Type)"
        ImGui.TextDisabled("Selected:");
        ImGui.SameLine();
        if (selected != null)
        {
            ImGui.TextUnformatted($"{selected.DisplayName} ({GetLabel(selected.GetType())})");
        }
        else
        {
            ImGui.TextDisabled("(none)");
        }

        // Thumbnail row
        const float barHeight = SelectionThumbSize + (TileSpacing * 2);
        ImGui.BeginChild("##SelectionBar", new NVector2(0, barHeight), ImGuiChildFlags.None,
            ImGuiWindowFlags.NoScrollbar);

        if (selected != null)
        {
            var selectedThumb = Eddy.Thumbnails.Get(selected);
            ImGuiX.Image(selectedThumb, new Vector2(SelectionThumbSize));
        }

        ImGui.SameLine();
        for (var i = 0; i < EddyEditor.MaxRecentEntries; i++)
        {
            ImGui.SameLine();
            ImGui.PushID(i);

            if (i < Eddy.RecentEntries.Count)
            {
                var recent = Eddy.RecentEntries[i];
                var thumb = Eddy.Thumbnails.Get(recent);

                if (ImGuiX.ImageButton("##recent", thumb, new Vector2(RecentThumbSize)))
                {
                    Eddy.Tool = Eddy.PickAndPaint(recent);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"{recent.DisplayName} ({GetLabel(recent.GetType())})");
                }
            }
            else
            {
                ImGui.BeginDisabled();
                ImGuiX.Button("##recent", new Vector2(RecentThumbSize));
                ImGui.EndDisabled();
            }

            ImGui.PopID();
        }

        ImGui.EndChild();
    }

    protected override Texture2D GetThumbnail(AssetEntry item)
    {
        return Eddy.Thumbnails.Get(item);
    }

    protected override string GetItemLabel(AssetEntry item)
    {
        return item.DisplayName;
    }

    protected override string GetStableId(AssetEntry entry)
    {
        return entry switch
        {
            AssetEntry.Trile trile => $"Trile:{trile.Id}:{trile.Path}",
            AssetEntry.ArtObject artObject => $"ArtObject:{artObject.Name}",
            AssetEntry.BackgroundPlane plane => $"BackgroundPlane:{plane.Name}",
            AssetEntry.NonPlayableCharacter npc => $"NPC:{npc.Name}",
            _ => entry.ToString()
        };
    }

    protected override bool IsSelected(AssetEntry item)
    {
        return Eddy.SelectedEntry == item;
    }

    protected override void Activate(AssetEntry item)
    {
        Eddy.Tool = Eddy.PickAndPaint(item);
    }

    private void OnProviderChanged()
    {
        _entries.Clear();
        LoadAssetEntries();
    }

    private void LoadAssetEntries()
    {
        var npcFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _entries.Clear();

        foreach (var (id, trile) in Eddy.TrileSet.Triles)
        {
            _entries.Add(new AssetEntry.Trile(trile.Name, Eddy.TrileSet.Name + "/" + trile.Name, id));
        }

        foreach (var file in Resources.Files)
        {
            var extension = Resources.GetExtension(file);
            if (file.StartsWith("Art Objects/", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".fezao.glb", StringComparison.OrdinalIgnoreCase))
            {
                if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
                {
                    var name = file.StartsWith("Art Objects/", StringComparison.OrdinalIgnoreCase)
                        ? file["Art Objects/".Length..]
                        : file;
                    _entries.Add(new AssetEntry.ArtObject(name));
                }
            }
            else if (file.StartsWith("Background Planes/", StringComparison.OrdinalIgnoreCase))
            {
                _entries.Add(new AssetEntry.BackgroundPlane(file["Background Planes/".Length..]));
            }
            else if (file.StartsWith("Character Animations/", StringComparison.OrdinalIgnoreCase) &&
                     !file.Contains("Metadata", StringComparison.OrdinalIgnoreCase))
            {
                var remainder = file["Character Animations/".Length..];
                var slashIndex = remainder.IndexOf('/');
                if (slashIndex >= 0)
                {
                    var name = remainder[..slashIndex];
                    if (npcFolders.Add(name))
                    {
                        _entries.Add(new AssetEntry.NonPlayableCharacter(name));
                    }
                }
            }
        }
    }

    private static string GetLabel(Type type)
    {
        if (type == typeof(AssetEntry.Trile)) return "Triles";
        if (type == typeof(AssetEntry.ArtObject)) return "Art Objects";
        if (type == typeof(AssetEntry.BackgroundPlane)) return "Planes";
        if (type == typeof(AssetEntry.NonPlayableCharacter)) return "NPCs/Critters";
        throw new ArgumentException($"{type} is not supported");
    }
}