using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Eddy;

public class InstanceBrowserSystem : ThumbnailBrowserSystem<InstanceId>
{
    private int _visibleCount;

    private int _totalCount;

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
            DrawBrowserToolbar("Filter instances...");
            ImGui.Separator();

            _visibleCount = 0;
            _totalCount = 0;
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

            ImGui.Separator();
            ImGui.TextDisabled($"{_visibleCount} visible / {_totalCount} total");

            ImGui.End();
        }

        if (!isOpen)
        {
            Eddy.ShowInstanceBrowser = false;
        }
    }

    private void DrawTab(string tabLabel, IReadOnlyList<InstanceId> instances)
    {
        if (!ImGui.BeginTabItem(tabLabel))
        {
            return;
        }

        var filtered = FilterInstances(tabLabel, instances);
        _visibleCount = filtered.Count;
        _totalCount = instances.Count;

        if (filtered.Count == 0)
        {
            ImGui.TextDisabled(instances.Count == 0 ? "(none)" : "No matching instances");
            ImGui.EndTabItem();
            return;
        }

        var gridHeight = -(ImGui.GetTextLineHeightWithSpacing() * 2f);
        DrawTileGrid($"##{tabLabel}Grid", filtered, new NVector2(0, gridHeight));
        ImGui.EndTabItem();
    }

    private List<InstanceId> FilterInstances(string tabLabel, IReadOnlyList<InstanceId> instances)
    {
        if (MatchesFilter(tabLabel))
        {
            return instances.ToList();
        }

        return instances
            .Where(instance => MatchesFilter(GetItemLabel(instance), instance.ToString()))
            .ToList();
    }

    protected override Texture2D GetThumbnail(InstanceId item)
    {
        return Eddy.Thumbnails.Get(item);
    }

    protected override string GetItemLabel(InstanceId item)
    {
        return item is InstanceId.Gomez ? "Gomez" : $"#{item.GetId()}";
    }

    protected override string GetStableId(InstanceId item)
    {
        return item.ToString();
    }

    protected override bool IsSelected(InstanceId item)
    {
        return Eddy.Selected is SelectionState.Instance selection && selection.Selected.Contains(item);
    }

    protected override void Activate(InstanceId item)
    {
        if (Eddy.Picked is PickingState.Waiting)
        {
            Eddy.Picked = new PickingState.Picked(item);
            return;
        }

        Eddy.Selected = new SelectionState.Instance([item]);
    }
}