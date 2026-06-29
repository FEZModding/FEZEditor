using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public class InstanceBrowserSystem : EddySystem
{
    private const float ThumbSize = 64f;

    private const float CellSpacing = 8f;

    private const float CellSize = ThumbSize + CellSpacing;

    public override void Draw()
    {
        if (!Eddy.ShowInstanceBrowser)
        {
            return;
        }

        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoCollapse;
        ImGuiX.SetNextWindowSize(new Vector2(500, 400), ImGuiCond.FirstUseEver);

        var isOpen = Eddy.ShowInstanceBrowser;
        if (ImGui.Begin("Instance Browser", ref isOpen, flags))
        {
            if (ImGui.BeginTabBar("##InstanceTabs"))
            {
                {
                    var trileGroups = Level.Groups.Keys.Order()
                        .Select(id => new InstanceId.TrileGroup(id))
                        .ToArray();

                    DrawTab("Trile Groups", trileGroups);
                }
                {
                    var artObjects = Level.ArtObjects.Keys.Order()
                        .Select(id => new InstanceId.ArtObject(id))
                        .ToList();

                    DrawTab("Art Objects", artObjects);
                }
                {
                    var backgroundPlanes = Level.BackgroundPlanes.Keys.Order()
                        .Select(id => new InstanceId.BackgroundPlane(id))
                        .ToList();

                    DrawTab("Background Planes", backgroundPlanes);
                }
                {
                    var npcs = Level.NonPlayerCharacters.Keys.Order()
                        .Select(id => new InstanceId.NonPlayableCharacter(id))
                        .ToList<InstanceId>();

                    npcs.Add(new InstanceId.Gomez());

                    DrawTab("Critters/NPCs", npcs);
                }
                {
                    var volumes = Level.Volumes.Keys.Order()
                        .Select(id => new InstanceId.Volume(id))
                        .ToList();

                    DrawTab("Volumes", volumes);
                }
                {
                    var paths = Level.Paths.Keys.Order()
                        .Select(id => new InstanceId.Path(id))
                        .ToList<InstanceId>();

                    paths.AddRange(Level.Groups
                        .Where(kv => kv.Value.Path != null)
                        .Select(kv => new InstanceId.GroupPath(kv.Key)));

                    DrawTab("Paths", paths);
                }

                ImGui.EndTabBar();
            }

            ImGui.End();
        }

        if (!isOpen)
        {
            Eddy.ShowInstanceBrowser = false;
        }
    }

    private unsafe void DrawTab(string tabLabel, IReadOnlyList<InstanceId> instances)
    {
        if (!ImGui.BeginTabItem(tabLabel))
        {
            return;
        }

        if (instances.Count == 0)
        {
            ImGui.TextDisabled("(none)");
            ImGui.EndTabItem();
            return;
        }

        var availWidth = ImGui.GetContentRegionAvail().X;
        var columns = Math.Max((int)(availWidth / CellSize), 1);
        var totalRows = (instances.Count + columns - 1) / columns;
        var style = ImGui.GetStyle();
        var rowHeight = ThumbSize + (style.FramePadding.Y * 2) + style.ItemSpacing.Y +
                        ImGui.GetTextLineHeight() + (style.CellPadding.Y * 2);

        if (!ImGui.BeginTable($"##{tabLabel}grid", columns,
                ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.EndTabItem();
            return;
        }

        var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
        clipper.Begin(totalRows, rowHeight);

        while (clipper.Step())
        {
            for (var row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);

                for (var col = 0; col < columns; col++)
                {
                    var i = (row * columns) + col;
                    if (i >= instances.Count)
                    {
                        break;
                    }

                    ImGui.TableSetColumnIndex(col);

                    var instance = instances.ElementAt(i);
                    var texture = Eddy.Thumbnails.Get(instance);
                    var cellWidth = ImGui.GetColumnWidth();

                    ImGui.PushID(i);

                    var padX = (cellWidth - ThumbSize) * 0.5f;
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padX);
                    if (ImGuiX.ImageButton("##sel", texture, new Vector2(ThumbSize)))
                    {
                        if (Eddy.Picked is PickingState.Waiting)
                        {
                            Eddy.Picked = new PickingState.Picked(instance);
                        }
                        else
                        {
                            var selected = new HashSet<InstanceId> { instance };
                            Eddy.Selected = new SelectionState.Instance(selected);
                        }
                    }

                    var label = instance is InstanceId.Gomez ? "Gomez" : $"#{instance.GetId()}";
                    var textSize = ImGui.CalcTextSize(label, true);
                    var labelPad = (cellWidth - textSize.X) * 0.5f;
                    if (labelPad > 0)
                    {
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + labelPad);
                    }

                    ImGui.TextUnformatted(label);

                    ImGui.PopID();
                }
            }
        }

        clipper.End();
        clipper.Destroy();
        ImGui.EndTable();
        ImGui.EndTabItem();
    }
}