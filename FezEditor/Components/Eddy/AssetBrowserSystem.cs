using FezEditor.Structure;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public class AssetBrowserSystem : EddySystem
{
    private const float ThumbSize = 64f;

    private const float RecentThumbSize = 48f;

    private const float CellSpacing = 8f;

    private const float CellSize = ThumbSize + CellSpacing;

    private const float LabelHeight = 20f;

    private const float RowHeight = CellSize + LabelHeight;

    private readonly HashSet<AssetEntry> _entries = new();

    private string _filterEntries = string.Empty;

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

            DrawFilter();
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

                            if (!string.IsNullOrEmpty(_filterEntries))
                            {
                                entries = entries.FindAll(ae =>
                                    ae.DisplayName.Contains(_filterEntries, StringComparison.OrdinalIgnoreCase));
                            }

                            DrawGrid(entries);

                            ImGui.EndTabItem();
                        }
                    }

                    ImGui.EndTabBar();
                }

                ImGui.EndChild();
            }

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
        const float barHeight = ThumbSize + (CellSpacing * 2);
        ImGui.BeginChild("##SelectionBar", new NVector2(0, barHeight), ImGuiChildFlags.None,
            ImGuiWindowFlags.NoScrollbar);

        if (selected != null)
        {
            var selectedThumb = Eddy.Thumbnails.Get(selected);
            ImGuiX.Image(selectedThumb, new Vector2(ThumbSize));
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

    private void DrawFilter()
    {
        ImGui.InputTextWithHint("", "Filter assets...", ref _filterEntries, 255);
        if (!string.IsNullOrEmpty(_filterEntries))
        {
            ImGui.SameLine();
            if (ImGui.Button($"{Lucide.X}"))
            {
                _filterEntries = string.Empty;
            }
        }
    }

    private void DrawGrid(IReadOnlyList<AssetEntry> entries)
    {
        var availWidth = ImGui.GetContentRegionAvail().X;
        var columns = Math.Max((int)(availWidth / CellSize), 1);
        var totalRows = (entries.Count + columns - 1) / columns;

        if (!ImGui.BeginTable("##grid", columns, ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchSame))
        {
            return;
        }

        var scrollToSelected = ImGui.IsWindowAppearing();

        for (var row = 0; row < totalRows; row++)
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None, RowHeight);

            for (var col = 0; col < columns; col++)
            {
                var i = (row * columns) + col;
                if (i >= entries.Count)
                {
                    break;
                }

                ImGui.TableSetColumnIndex(col);

                var entry = entries[i];
                var isSelected = Eddy.SelectedEntry == entry;
                var texture = Eddy.Thumbnails.Get(entry);
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
                    Eddy.Tool = Eddy.PickAndPaint(entry);
                }

                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    Eddy.Tool = Eddy.PickAndPaint(entry);
                }

                // Label wrapped and centered below thumbnail
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + cellWidth);
                var textSize = ImGui.CalcTextSize(entry.DisplayName, true);
                var labelPad = (cellWidth - textSize.X) * 0.5f;
                if (labelPad > 0)
                {
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + labelPad);
                }

                ImGui.TextUnformatted(entry.DisplayName);
                ImGui.PopTextWrapPos();

                if (isSelected && scrollToSelected)
                {
                    ImGui.SetScrollHereY(0.5f);
                }

                ImGui.PopID();
            }
        }

        ImGui.EndTable();
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
            if (file.StartsWith("Art Objects/", StringComparison.OrdinalIgnoreCase))
            {
                var extension = Resources.GetExtension(file);
                if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
                {
                    _entries.Add(new AssetEntry.ArtObject(file["Art Objects/".Length..]));
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