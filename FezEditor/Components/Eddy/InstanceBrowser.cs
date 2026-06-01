using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Eddy;

public class InstanceBrowser : IDisposable
{
    private const float ThumbSize = 64f;

    private const float CellSpacing = 8f;

    private const float CellSize = ThumbSize + CellSpacing;

    private const float LabelHeight = 20f;

    private const float RowHeight = CellSize + LabelHeight;

    private readonly Level _level;

    private readonly AssetBrowser _assetBrowser;

    private Texture2D _placeholder = null!;

    private (EddyContext context, int id)? _pendingSelection;

    public InstanceBrowser(Level level, AssetBrowser assetBrowser)
    {
        _level = level;
        _assetBrowser = assetBrowser;
    }

    public void LoadContent(IContentManager content)
    {
        _placeholder = content.Load<Texture2D>("Missing");
    }

    public bool Select(out (EddyContext context, int id) selection)
    {
        if (_pendingSelection.HasValue)
        {
            selection = _pendingSelection.Value;
            return true;
        }

        selection = (EddyContext.Default, -1);
        return false;
    }

    public void Consume()
    {
        _pendingSelection = null;
    }

    public void Draw()
    {
        if (ImGui.BeginTabBar("##InstanceTabs"))
        {
            DrawTab("Groups", _level.Groups.Keys.Order()
                .Select(id =>
                {
                    var group = _level.Groups[id];
                    var firstTrile = group.Triles.FirstOrDefault();
                    Texture2D? thumb = null;
                    if (firstTrile != null)
                    {
                        var trileName = _assetBrowser.GetTrileNameById(firstTrile.TrileId);
                        if (trileName != null)
                        {
                            thumb = _assetBrowser.GetThumbnail(AssetType.Trile, trileName);
                        }
                    }

                    return (EddyContext.Trile, id, thumb, $"#{id}");
                }).ToList());

            DrawTab("Art Objects", _level.ArtObjects.Keys.Order()
                .Select(id => (EddyContext.ArtObject, id,
                    _assetBrowser.GetThumbnail(AssetType.ArtObject, _level.ArtObjects[id].Name),
                    $"#{id}"))
                .ToList());

            DrawTab("Planes", _level.BackgroundPlanes.Keys.Order()
                .Select(id => (EddyContext.BackgroundPlane, id,
                    _assetBrowser.GetThumbnail(AssetType.BackgroundPlane, _level.BackgroundPlanes[id].TextureName),
                    $"#{id}"))
                .ToList());

            var npcEntries = _level.NonPlayerCharacters
                .OrderBy(kv => kv.Key)
                .Select(kv => (EddyContext.NonPlayableCharacter, kv.Key,
                    _assetBrowser.GetThumbnail(AssetType.NonPlayableCharacter, kv.Value.Name),
                    $"#{kv.Key}"))
                .Prepend((EddyContext.Gomez, 0, _assetBrowser.GetThumbnail(AssetType.NonPlayableCharacter, "Gomez"), "Gomez"))
                .ToList();
            DrawTab("Critters/NPCs", npcEntries);

            DrawTab("Volumes", _level.Volumes.Keys.Order()
                .Select(id => (EddyContext.Volume, id, (Texture2D?)null, $"#{id}"))
                .ToList());

            DrawTab("Paths", _level.Paths.Keys.Order()
                .Select(id => (EddyContext.Path, id, (Texture2D?)null, $"#{id}"))
                .ToList());

            ImGui.EndTabBar();
        }
    }

    private unsafe void DrawTab(string label, List<(EddyContext context, int id, Texture2D? thumb, string label)> entries)
    {
        if (!ImGui.BeginTabItem(label))
        {
            return;
        }

        if (entries.Count == 0)
        {
            ImGui.TextDisabled("(none)");
            ImGui.EndTabItem();
            return;
        }

        var availWidth = ImGui.GetContentRegionAvail().X;
        var columns = Math.Max((int)(availWidth / CellSize), 1);
        var totalRows = (entries.Count + columns - 1) / columns;

        if (!ImGui.BeginTable($"##{label}grid", columns, ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.EndTabItem();
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
                    var i = (row * columns) + col;
                    if (i >= entries.Count)
                    {
                        break;
                    }

                    ImGui.TableSetColumnIndex(col);

                    var (context, id, thumb, entryLabel) = entries[i];
                    var texture = thumb ?? _placeholder;
                    var cellWidth = ImGui.GetColumnWidth();

                    ImGui.PushID(i);

                    var padX = (cellWidth - ThumbSize) * 0.5f;
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padX);
                    if (ImGuiX.ImageButton("##sel", texture, new Vector2(ThumbSize)))
                    {
                        _pendingSelection = (context, id);
                    }

                    var textSize = ImGui.CalcTextSize(entryLabel, true);
                    var labelPad = (cellWidth - textSize.X) * 0.5f;
                    if (labelPad > 0)
                    {
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + labelPad);
                    }

                    ImGui.TextUnformatted(entryLabel);

                    ImGui.PopID();
                }
            }
        }

        clipper.End();
        clipper.Destroy();
        ImGui.EndTable();
        ImGui.EndTabItem();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}