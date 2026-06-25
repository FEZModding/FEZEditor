using FezEditor.Actors;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public class TranslateToolSystem : EddySystem
{
    private readonly Gizmo _gizmo;

    public TranslateToolSystem(Gizmo gizmo)
    {
        _gizmo = gizmo;
    }

    public override void Update()
    {
        if (Eddy.Tool is not ToolState.Translate tool)
        {
            return;
        }

        switch (Eddy.Selected)
        {
            case SelectionState.Trile triles:
                UpdateTrileTranslate(tool, triles);
                break;

            case SelectionState.TrileGroup trileGroup:
                UpdateTrileGroupTranslation(tool, trileGroup);
                break;

            case SelectionState.Instance instances:
                UpdateInstancesTranslate(tool, instances.Selected);
                break;

            case SelectionState.Path path:
                UpdatePathTranslate(tool, path);
                break;
        }
    }

    public override bool IsToolEnabled(ToolState tool)
    {
        return tool is ToolState.Translate && Eddy.Selected switch
        {
            SelectionState.Trile { Selected.Count: > 0 } => true,
            SelectionState.TrileGroup { Selected.Count: > 0 } => true,
            SelectionState.Instance { Selected.Count: > 0 } => true,
            SelectionState.Path { Waypoints.Count: > 0 } => true,
            _ => false
        };
    }

    #region Triles and Trile Groups

    private void UpdateTrileGroupTranslation(ToolState.Translate tool, SelectionState.TrileGroup selection)
    {
        var emplacements = new List<TrileEmplacement>();
        foreach (var groupId in selection.Selected)
        {
            if (Level.Groups.TryGetValue(groupId, out var group))
            {
                foreach (var trile in group.Triles)
                {
                    var emplacement = new TrileEmplacement(trile.Position);
                    if (!emplacements.Contains(emplacement))
                    {
                        emplacements.Add(emplacement);
                    }
                }
            }
        }

        if (emplacements.Count == 0)
        {
            return;
        }

        var trileSelection = new SelectionState.Trile(emplacements, FaceOrientation.Top, emplacements[0]);
        UpdateTrileTranslate(tool, trileSelection);
    }

    private void UpdateTrileTranslate(ToolState.Translate tool, SelectionState.Trile selection)
    {
        if (selection.Selected.Count == 0)
        {
            return;
        }

        var centroid = ComputeTrileCentroid(selection);
        var bounds = ComputeTrilePositionBounds(selection);
        var before = centroid;

        if (_gizmo.Translate(ref centroid, bounds))
        {
            var delta = centroid - before;
            foreach (var emplacement in selection.Selected)
            {
                SetTrilePosition(emplacement, position => (position.ToXna() + delta)
                    .ClampWithinEmplacement(emplacement)
                    .ToRepacker()
                );
            }

            Eddy.Visualize(new InstanceId.CollisionMap());
            Eddy.Visualize(new InstanceId.PickableBounds());
        }

        if (_gizmo.DragStarted)
        {
            tool.Clear();
            tool.HistoryScope = Eddy.History.BeginScope("Translate Trile");
        }

        if (_gizmo.DragEnded)
        {
            tool.Clear();
        }

        #region Reset local translation of triles

        Status.AddHints(
            ("R", "Reset")
        );

        if (ImGui.IsKeyPressed(ImGuiKey.R) && tool.HistoryScope == null)
        {
            using (Eddy.History.BeginScope("Reset Translate Trile"))
            {
                foreach (var emplacement in selection.Selected)
                {
                    SetTrilePosition(emplacement, _ => emplacement.AsVector());
                }

                Eddy.Visualize(new InstanceId.CollisionMap());
                Eddy.Visualize(new InstanceId.PickableBounds());
            }
        }

        #endregion
    }

    private Vector3 ComputeTrileCentroid(SelectionState.Trile selection)
    {
        var instances = selection.Selected
            .Select(GetActiveTrile)
            .Where(ti => ti != null)
            .ToList();

        return instances.Count == 0
            ? Vector3.Zero
            : instances.Aggregate(Vector3.Zero, (sum, ti) => sum + ti!.Position.ToXna()) / instances.Count;
    }

    private static BoundingBox? ComputeTrilePositionBounds(SelectionState.Trile selection)
    {
        var count = selection.Selected.Count;
        if (count == 0)
        {
            return new BoundingBox(Vector3.Zero, Vector3.Zero);
        }

        var min = Vector3.Zero;
        var max = Vector3.Zero;

        foreach (var emplacement in selection.Selected)
        {
            var bounds = Mathz.GetEmplacementPositionBounds(emplacement);
            min += bounds.Min;
            max += bounds.Max;
        }

        return new BoundingBox(min / count, max / count);
    }

    private void SetTrilePosition(TrileEmplacement emplacement, Func<RVector3, RVector3> setter)
    {
        var active = GetActiveTrile(emplacement);
        if (active == null)
        {
            return;
        }

        var before = active.Clone();
        active.Position = setter(active.Position);

        var after = active.Clone();
        InstanceId instance = Eddy.OverlapIndex <= 0
            ? new InstanceId.TrileChange(emplacement, before, after)
            : new InstanceId.TrileOverlapChange(emplacement, Eddy.OverlapIndex - 1, before, after);

        Eddy.Visualize(instance);
    }

    private TrileInstance? GetActiveTrile(TrileEmplacement emplacement)
    {
        if (!Level.Triles.TryGetValue(emplacement, out var trile))
        {
            return null;
        }

        if (Eddy.OverlapIndex < 1)
        {
            return trile;
        }

        var slot = Eddy.OverlapIndex - 1;
        if (trile.OverlappedTriles == null || slot >= trile.OverlappedTriles.Count)
        {
            return null;
        }

        return trile.OverlappedTriles[slot];
    }

    #endregion

    #region Instances

    private void UpdateInstancesTranslate(ToolState.Translate tool, HashSet<InstanceId> selection)
    {
        if (selection.Count < 1)
        {
            return;
        }

        var centroid = ComputeInstanceCentroid(selection);
        var before = centroid;

        if (_gizmo.Translate(ref centroid))
        {
            var delta = centroid - before;
            foreach (var id in selection)
            {
                if (ApplyDeltaToInstancePosition(id, delta))
                {
                    Eddy.Visualize(id);
                }
            }
        }

        if (_gizmo.DragStarted)
        {
            tool.Clear();
            tool.HistoryScope = Eddy.History.BeginScope("Translate Instance(s)");
        }

        if (_gizmo.DragEnded)
        {
            tool.Clear();
        }
    }

    private Vector3 ComputeInstanceCentroid(HashSet<InstanceId> instances)
    {
        var count = 0;
        var sum = Vector3.Zero;

        foreach (var instance in instances)
        {
            if (TryGetInstancePosition(instance, out var position))
            {
                sum += position;
                count++;
            }
        }

        return count > 0
            ? sum / count
            : Vector3.Zero;
    }

    private bool TryGetInstancePosition(InstanceId id, out Vector3 position)
    {
        switch (id)
        {
            case InstanceId.ArtObject ao
                when Level.ArtObjects.TryGetValue(ao.Id, out var instance):
                position = instance.Position.ToXna();
                return true;

            case InstanceId.BackgroundPlane bp
                when Level.BackgroundPlanes.TryGetValue(bp.Id, out var instance):
                position = instance.Position.ToXna();
                return true;

            case InstanceId.NonPlayableCharacter npc
                when Level.NonPlayerCharacters.TryGetValue(npc.Id, out var instance):
                position = instance.Position.ToXna();
                return true;

            case InstanceId.Volume v
                when Level.Volumes.TryGetValue(v.Id, out var instance):
                position = (instance.From + instance.To).ToXna() / 2f;
                return true;

            case InstanceId.Gomez when Level.StartingFace != null:
                position = Level.StartingFace.Id.AsVector().ToXna() + Vector3.Up;
                return true;

            default:
                position = Vector3.Zero;
                return false;
        }
    }

    private bool ApplyDeltaToInstancePosition(InstanceId id, Vector3 delta)
    {
        switch (id)
        {
            case InstanceId.ArtObject ao
                when Level.ArtObjects.TryGetValue(ao.Id, out var instance):
                instance.Position = (instance.Position.ToXna() + delta).ToRepacker();
                return true;

            case InstanceId.BackgroundPlane bp
                when Level.BackgroundPlanes.TryGetValue(bp.Id, out var instance):
                instance.Position = (instance.Position.ToXna() + delta).ToRepacker();
                return true;

            case InstanceId.NonPlayableCharacter npc
                when Level.NonPlayerCharacters.TryGetValue(npc.Id, out var instance):
                instance.Position = (instance.Position.ToXna() + delta).ToRepacker();
                return true;

            case InstanceId.Volume v
                when Level.Volumes.TryGetValue(v.Id, out var instance):
                instance.From += delta.ToRepacker();
                instance.To += delta.ToRepacker();
                return true;

            case InstanceId.Gomez when Level.StartingFace != null:
                var position = Level.StartingFace.Id.AsVector().ToXna() + Vector3.Up + delta;
                Level.StartingFace.Id = new TrileEmplacement((position - Vector3.Up).ToRepacker());
                return true;

            default:
                return false;
        }
    }

    #endregion

    #region Path waypoints

    private void UpdatePathTranslate(ToolState.Translate tool, SelectionState.Path selection)
    {
        if (selection.Waypoints.Count == 0 ||
            !TryGetPath(selection.Selected, out var path, out var offset))
        {
            return;
        }

        var centroid = ComputePathWaypointCentroid(path, selection.Waypoints, offset);
        var before = centroid;

        if (_gizmo.Translate(ref centroid))
        {
            var delta = centroid - before;
            foreach (var index in selection.Waypoints.Where(i => i >= 0 && i < path.Segments.Count))
            {
                var segment = path.Segments[index];
                segment.Destination = (segment.Destination.ToXna() + delta).ToRepacker();
            }

            Eddy.Visualize(selection.Selected);
        }

        if (_gizmo.DragStarted)
        {
            tool.Clear();
            tool.HistoryScope = Eddy.History.BeginScope("Translate Path Waypoint");
        }

        if (_gizmo.DragEnded)
        {
            tool.Clear();
        }
    }

    private bool TryGetPath(InstanceId instance, out MovementPath path, out Vector3 offset)
    {
        switch (instance)
        {
            case InstanceId.Path p
                when Level.Paths.TryGetValue(p.Id, out var pathInstance):
                path = pathInstance;
                offset = Vector3.Zero;
                return true;

            case InstanceId.GroupPath gp
                when Level.Groups.TryGetValue(gp.GroupId, out var group) && group.Path != null:
                path = group.Path;
                offset = ComputeGroupPathOffset(group);
                return true;

            default:
                path = null!;
                offset = Vector3.Zero;
                return false;
        }
    }

    private static Vector3 ComputeGroupPathOffset(TrileGroup group)
    {
        if (group.Triles.Count == 0)
        {
            return Vector3.Zero;
        }

        return group.Triles
            .Select(t => t.Position.ToXna())
            .Aggregate(Vector3.Zero, (sum, p) => sum + p) / group.Triles.Count;
    }

    private static Vector3 ComputePathWaypointCentroid(MovementPath path, HashSet<int> waypoints, Vector3 offset)
    {
        var count = 0;
        var sum = Vector3.Zero;

        foreach (var index in waypoints)
        {
            if (index < 0 || index >= path.Segments.Count)
            {
                continue;
            }

            sum += offset + path.Segments[index].Destination.ToXna();
            count++;
        }

        return count > 0
            ? sum / count
            : Vector3.Zero;
    }

    #endregion
}