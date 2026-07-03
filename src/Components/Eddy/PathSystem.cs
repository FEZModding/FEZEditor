using FezEditor.Actors;
using FezEditor.Components.Eddy;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public class PathSystem : EddySystem
{
    private static readonly Color DefaultColor = new(1f, 0.5f, 0f, 0.8f);

    public override void Initialize()
    {
        foreach (var id in Level.Paths.Keys.Where(k => k != EddyEditor.InvalidId))
        {
            Visualize(new InstanceId.Path(id));
        }

        foreach (var (id, group) in Level.Groups.Where(kv => kv.Key != EddyEditor.InvalidId))
        {
            if (group.Path != null)
            {
                Visualize(new InstanceId.GroupPath(id));
            }
        }
    }

    public override void Visualize(InstanceId instanceId)
    {
        if (instanceId is not InstanceId.Path or InstanceId.GroupPath)
        {
            return;
        }

        MovementPath path;
        Vector3 offset;
        string name;

        switch (instanceId)
        {
            case InstanceId.Path p:
                {
                    if (!Level.Paths.TryGetValue(p.Id, out var levelPath))
                    {
                        Eddy.Registry.Destroy(instanceId);
                        return;
                    }

                    name = $"{p.Id}: Path";
                    path = levelPath;
                    offset = Vector3.Zero;
                    break;
                }

            case InstanceId.GroupPath gp:
                {
                    if (!Level.Groups.TryGetValue(gp.GroupId, out var group) || group.Path == null)
                    {
                        Eddy.Registry.Destroy(instanceId);
                        return;
                    }

                    name = $"Group {gp.GroupId}: Path";
                    path = group.Path;
                    offset = ComputeGroupOffset(group);
                    break;
                }

            default:
                return;
        }

        var actor = Eddy.Registry.GetOrCreateActor(instanceId);
        actor.Name = name;
        actor.Visible = Eddy.Visuals.HasFlag(EddyVisuals.Paths);

        if (!actor.TryGetComponent<PathMesh>(out var mesh))
        {
            mesh = actor.AddComponent<PathMesh>();
        }

        mesh!.Waypoints = path.Segments.Select(ps => offset + ps.Destination.ToXna()).ToList();
        mesh.WaypointColors = Enumerable.Repeat(DefaultColor, mesh.Waypoints.Count).ToList();
    }

    public override void Inspect(params HashSet<InstanceId> instanceIds)
    {
        if (!instanceIds.TryGetSingle<InstanceId>(out var instanceId))
        {
            return;
        }

        string label;
        MovementPath? path;

        switch (instanceId)
        {
            case InstanceId.Path p:
                {
                    label = $"Level Path: ID={p.Id}";
                    path = Level.Paths[p.Id];
                    break;
                }

            case InstanceId.GroupPath gp:
                {
                    label = $"Group Path: ID={gp.GroupId}";
                    path = Level.Groups[gp.GroupId].Path!;
                    break;
                }

            default:
                return;
        }

        ImGui.Text(label);

        var endBehavior = (int)path.EndBehavior;
        var endBehaviorNames = Enum.GetNames<PathEndBehavior>();
        if (ImGui.Combo("End Behavior", ref endBehavior, endBehaviorNames, endBehaviorNames.Length))
        {
            using (Eddy.History.BeginScope("Edit Path End Behavior"))
            {
                path.EndBehavior = (PathEndBehavior)endBehavior;
            }
        }

        var isSpline = path.IsSpline;
        if (ImGui.Checkbox("Is Spline", ref isSpline))
        {
            using (Eddy.History.BeginScope("Edit Path Is Spline"))
            {
                path.IsSpline = isSpline;
            }
        }

        var needsTrigger = path.NeedsTrigger;
        if (ImGui.Checkbox("Needs Trigger", ref needsTrigger))
        {
            using (Eddy.History.BeginScope("Edit Path Needs Trigger"))
            {
                path.NeedsTrigger = needsTrigger;
            }
        }

        var offsetSec = path.OffsetSeconds;
        if (ImGui.DragFloat("Offset Seconds", ref offsetSec))
        {
            using (Eddy.History.BeginScope("Edit Path Offset Seconds"))
            {
                path.OffsetSeconds = offsetSec;
            }
        }

        var soundName = path.SoundName.EmptyIfNull();
        if (ImGui.InputText("Sound Name", ref soundName, 255))
        {
            using (Eddy.History.BeginScope("Edit Path Sound Name"))
            {
                path.SoundName = soundName.NullIfEmpty();
            }
        }

        var saveTrigger = path.SaveTrigger;
        if (ImGui.Checkbox("Save Trigger", ref saveTrigger))
        {
            using (Eddy.History.BeginScope("Edit Path Save Trigger"))
            {
                path.SaveTrigger = saveTrigger;
            }
        }

        ImGui.SeparatorText($"Segments ({path.Segments.Count})");
        if (ImGui.Button($"{Lucide.Plus} Add Segment##PathSegment"))
        {
            using (Eddy.History.BeginScope("Add Path Segment"))
            {
                var destination = path.Segments.Count == 0
                    ? Vector3.UnitX
                    : path.Segments[^1].Destination.ToXna() + Vector3.Down;

                path.Segments.Add(new PathSegment
                {
                    Destination = destination.ToRepacker()
                });

                Eddy.Selected = new SelectionState.Path(instanceId, [path.Segments.Count - 1]);
            }
        }

        for (var i = 0; i < path.Segments.Count; i++)
        {
            var seg = path.Segments[i];
            var isSegSelected = Eddy.Selected is SelectionState.Path p &&
                                p.Selected == instanceId &&
                                p.Waypoints.Contains(i);

            ImGui.PushID(i);
            if (isSegSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Header, new NVector4(0.6f, 0.5f, 0f, 1f));
            }

            var isOpen = ImGui.CollapsingHeader($"Segment {i}");
            var isHeaderClicked = ImGui.IsItemClicked();
            if (isOpen)
            {
                var isDeleted = false;
                const ImGuiChildFlags flags = ImGuiChildFlags.Border |
                                              ImGuiChildFlags.AutoResizeY |
                                              ImGuiChildFlags.AutoResizeX;
                if (ImGuiX.BeginChild($"##Segment_{i}", Vector2.Zero, flags))
                {
                    if (ImGui.Button($"{Lucide.Trash2} Delete Segment"))
                    {
                        isDeleted = true;
                    }

                    DrawSegmentProperties(seg);
                }

                ImGui.EndChild();

                if (isDeleted)
                {
                    using (Eddy.History.BeginScope("Delete Path Waypoint"))
                    {
                        path.Segments.RemoveAt(i);
                    }

                    if (Eddy.Selected is SelectionState.Path p1 && p1.Selected == instanceId)
                    {
                        var selected = new HashSet<int>();
                        foreach (var waypoint in p1.Waypoints)
                        {
                            if (waypoint < i)
                            {
                                selected.Add(waypoint);
                            }
                            else if (waypoint > i)
                            {
                                selected.Add(waypoint - 1);
                            }
                        }

                        Eddy.Selected = new SelectionState.Path(instanceId, selected);
                    }

                    if (isSegSelected)
                    {
                        ImGui.PopStyleColor();
                    }

                    ImGui.PopID();
                    break;
                }
            }

            if (isHeaderClicked)
            {
                var current = Eddy.Selected is SelectionState.Path path2 && path2.Selected == instanceId
                    ? path2.Waypoints
                    : [];
                var selected = ImGui.GetIO().KeyShift ? new HashSet<int>(current) : [];
                selected.Add(i);
                Eddy.Selected = new SelectionState.Path(instanceId, selected);
            }

            if (isSegSelected)
            {
                ImGui.PopStyleColor();
            }

            ImGui.PopID();
        }
    }

    private void DrawSegmentProperties(PathSegment seg)
    {
        var dest = seg.Destination.ToXna();
        if (ImGuiX.DragFloat3("Destination", ref dest, 0.01f))
        {
            using (Eddy.History.BeginScope("Edit Path Segment Destination"))
            {
                seg.Destination = dest.ToRepacker();
            }
        }

        var duration = seg.Duration;
        if (ImGuiX.TimeSpanInput("Duration", ref duration))
        {
            using (Eddy.History.BeginScope("Edit Path Segment Duration"))
            {
                seg.Duration = duration;
            }
        }

        var waitTimeOnStart = seg.WaitTimeOnStart;
        if (ImGuiX.TimeSpanInput("Wait Time On Start", ref waitTimeOnStart))
        {
            using (Eddy.History.BeginScope("Edit Path Segment Wait Time On Start"))
            {
                seg.WaitTimeOnStart = waitTimeOnStart;
            }
        }

        var waitTimeOnFinish = seg.WaitTimeOnFinish;
        if (ImGuiX.TimeSpanInput("Wait Time On Finish", ref waitTimeOnFinish))
        {
            using (Eddy.History.BeginScope("Edit Path Segment Wait Time On Finish"))
            {
                seg.WaitTimeOnFinish = waitTimeOnFinish;
            }
        }

        var acceleration = seg.Acceleration;
        if (ImGui.DragFloat("Acceleration", ref acceleration, 0.01f))
        {
            using (Eddy.History.BeginScope("Edit Path Segment Acceleration"))
            {
                seg.Acceleration = acceleration;
            }
        }

        var deceleration = seg.Deceleration;
        if (ImGui.DragFloat("Deceleration", ref deceleration, 0.01f))
        {
            using (Eddy.History.BeginScope("Edit Path Segment Deceleration"))
            {
                seg.Deceleration = deceleration;
            }
        }

        var jitterFactor = seg.JitterFactor;
        if (ImGui.DragFloat("Jitter Factor", ref jitterFactor, 0.01f))
        {
            using (Eddy.History.BeginScope("Edit Path Segment Jitter Factor"))
            {
                seg.JitterFactor = jitterFactor;
            }
        }

        var orientation = seg.Orientation.ToXna().ToYawPitchRollDegrees();
        if (ImGuiX.DragFloat3("Orientation (Yaw, Pitch, Roll)", ref orientation, 1f, -180f, 180f, "%.1f"))
        {
            using (Eddy.History.BeginScope("Edit Path Segment Orientation"))
            {
                seg.Orientation = Mathz.FromYawPitchRollDegrees(orientation).ToRepacker();
            }
        }

        var hasCustomData = seg.CustomData != null;
        if (ImGui.Checkbox("Custom Camera Data", ref hasCustomData))
        {
            using (Eddy.History.BeginScope("Edit Path Segment Custom Data"))
            {
                seg.CustomData = hasCustomData ? new CameraNodeData() : null;
            }
        }

        if (seg.CustomData is { } customData)
        {
            var perspective = customData.Perspective;
            if (ImGui.Checkbox("Perspective", ref perspective))
            {
                using (Eddy.History.BeginScope("Edit Path Segment Custom Data Perspective"))
                {
                    customData.Perspective = perspective;
                }
            }

            var pixelsPerTrixel = customData.PixelsPerTrixel;
            if (ImGui.InputInt("Pixels Per Trixel", ref pixelsPerTrixel))
            {
                using (Eddy.History.BeginScope("Edit Path Segment Custom Data Pixels Per Trixel"))
                {
                    customData.PixelsPerTrixel = pixelsPerTrixel;
                }
            }

            var soundName = customData.SoundName.EmptyIfNull();
            if (ImGui.InputText("Sound Name", ref soundName, 255))
            {
                using (Eddy.History.BeginScope("Edit Path Segment Custom Data Sound Name"))
                {
                    customData.SoundName = soundName.NullIfEmpty();
                }
            }
        }
    }

    private static Vector3 ComputeGroupOffset(TrileGroup group)
    {
        return group.Triles
                   .Select(ti => ti.Position.ToXna())
                   .Aggregate(Vector3.Zero, (sum, pos) => sum + pos)
               / group.Triles.Count;
    }
}