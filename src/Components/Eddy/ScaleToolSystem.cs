using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public class ScaleToolSystem : EddySystem
{
    private readonly Gizmo _gizmo;

    public ScaleToolSystem(Gizmo gizmo)
    {
        _gizmo = gizmo;
    }

    public override void Update()
    {
        if (Eddy.Tool is not ToolState.Scale tool)
        {
            return;
        }

        // Overlapped triles cannot be scaled, but the tool still handles selection input
        if (Eddy is not { OverlapIndex: > 0, Selected: SelectionState.Trile or SelectionState.TrileGroup })
        {
            switch (Eddy.Selected)
            {
                case SelectionState.Trile triles:
                    UpdateTrileScale(tool, triles);
                    break;

                case SelectionState.TrileGroup trileGroup:
                    UpdateTrileGroupScale(tool, trileGroup);
                    break;

                case SelectionState.Instance instances:
                    UpdateInstanceScale(tool, instances.Selected);
                    break;
            }
        }

        // Keep an active transform intact until its gizmo drag has finished
        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            if (tool.HistoryScope is null)
            {
                Eddy.Tool = new ToolState.Select();
            }

            return;
        }

        // Only an unhandled click on empty viewport space clears the selection
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
            !ImGui.GetIO().KeyShift &&
            Eddy.Frame.AllowsSelection &&
            Eddy.Hovered is null &&
            !_gizmo.IsActive)
        {
            Eddy.Selected = new SelectionState.Empty();
            Eddy.Tool = new ToolState.Select();
        }
    }

    public override bool IsToolEnabled(ToolState tool)
    {
        return tool is ToolState.Scale && Eddy.Selected switch
        {
            SelectionState.Trile { Selected.Count: > 0 } => Eddy.OverlapIndex == 0,
            SelectionState.TrileGroup { Selected.Count: > 0 } => Eddy.OverlapIndex == 0,
            SelectionState.Instance i => i.Selected.Any(id => id is InstanceId.ArtObject or
                InstanceId.BackgroundPlane or
                InstanceId.Volume),
            _ => false
        };
    }

    #region Triles and Trile Groups

    private void UpdateTrileGroupScale(ToolState.Scale tool, SelectionState.TrileGroup selection)
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

        var face = Eddy.Hovered is { Instance: InstanceId.TrileGroup g, Face: var hoveredFace } &&
                   selection.Selected.Contains(g.Id)
            ? hoveredFace
            : Eddy.HoveredTrile?.Face ?? FaceOrientation.Top;

        UpdateTrileScale(tool, new SelectionState.Trile(emplacements, face, emplacements[0]));
    }

    private void UpdateTrileScale(ToolState.Scale tool, SelectionState.Trile selection)
    {
        if (selection.Selected.Count == 0)
        {
            return;
        }

        var centroid = ComputeTrileCentroid(selection.Selected);
        var faceDirection = selection.Face.AsVector();

        var selectionMin = selection.Selected.Aggregate(
            new Vector3(float.MaxValue),
            (min, emplacement) => Vector3.Min(min, emplacement.AsVector().ToXna()));

        var disabled = (faceDirection.X < 0 && selectionMin.X <= 0) ||
                       (faceDirection.Y < 0 && selectionMin.Y <= 0) ||
                       (faceDirection.Z < 0 && selectionMin.Z <= 0);

        if (_gizmo.ScaleFace(centroid, selection.Face, out var delta, disabled))
        {
            var steps = (int)MathF.Round(delta);
            if (steps != tool.PreviousSteps)
            {
                var dir = steps > tool.PreviousSteps ? 1 : -1;
                for (var s = tool.PreviousSteps; s != steps; s += dir)
                {
                    var step = dir > 0 ? s + dir : s;
                    if (dir > 0)
                    {
                        AddTrileScaleStep(tool, step);
                    }
                    else
                    {
                        RemoveTrileScaleStep(tool, step);
                    }
                }

                tool.PreviousSteps = steps;
                selection.Selected.Clear();
                foreach (var (emplacement, _, _) in tool.Snapshot)
                {
                    var target = emplacement.Add((tool.Direction * tool.PreviousSteps).ToRepacker());
                    if (Level.Triles.ContainsKey(target))
                    {
                        selection.Selected.Add(target);
                    }
                }

                Eddy.Visualize(new InstanceId.CollisionMap());
                Eddy.Visualize(new InstanceId.PickableBounds());
                Eddy.Visualize(new InstanceId.LevelBounds());
                Eddy.Visualize(new InstanceId.Sky());
                Eddy.Visualize(new InstanceId.Liquid());
                Eddy.Visualize(new InstanceId.Rain());
            }
        }

        if (_gizmo.DragStarted)
        {
            tool.Clear();
            tool.HistoryScope = Eddy.History.BeginScope("Scale Triles");
            tool.Direction = new Vector3I(faceDirection);

            foreach (var emplacement in selection.Selected)
            {
                if (Level.Triles.TryGetValue(emplacement, out var trile))
                {
                    tool.Snapshot.Add((emplacement, trile.TrileId, trile.PhiLight));
                }
            }
        }

        if (_gizmo.DragEnded)
        {
            tool.Clear();
        }
    }

    private void AddTrileScaleStep(ToolState.Scale tool, int step)
    {
        var groups = Level.GetEmplacementGroups();
        foreach (var (emplacement, trileId, phiLight) in tool.Snapshot)
        {
            var target = emplacement.Add((tool.Direction * step).ToRepacker());
            if (target.X < 0 || target.Y < 0 || target.Z < 0 || Level.Triles.ContainsKey(target))
            {
                continue;
            }

            var instance = new TrileInstance
            {
                Position = target.AsVector(),
                TrileId = trileId,
                PhiLight = phiLight
            };

            Level.Triles[target] = instance;
            ExpandLevelBounds(target);

            if (groups.TryGetValue(emplacement, out var groupId) &&
                Level.Groups.TryGetValue(groupId, out var group))
            {
                group.Triles.Add(instance);
            }

            Eddy.Visualize(new InstanceId.TrileChange(target, null, instance.Clone()));
        }
    }

    private void RemoveTrileScaleStep(ToolState.Scale tool, int step)
    {
        foreach (var (emplacement, _, _) in tool.Snapshot)
        {
            var target = emplacement.Add((tool.Direction * step).ToRepacker());
            if (Level.Triles.Remove(target, out var removed))
            {
                foreach (var group in Level.Groups.Values)
                {
                    group.Triles.RemoveAll(trile => trile.Position.Equals(removed.Position));
                }

                Eddy.Visualize(new InstanceId.TrileChange(target, removed.Clone(), null));
            }
        }
    }

    private Vector3 ComputeTrileCentroid(IReadOnlyCollection<TrileEmplacement> emplacements)
    {
        var sum = Vector3.Zero;
        var count = 0;

        foreach (var emplacement in emplacements)
        {
            if (Level.Triles.TryGetValue(emplacement, out var trile))
            {
                sum += trile.Position.ToXna();
                count++;
            }
        }

        return count > 0 ? sum / count : Vector3.Zero;
    }

    private void ExpandLevelBounds(TrileEmplacement emplacement)
    {
        var oldSize = Level.Size.ToXna();
        var newSize = emplacement.AsVector().ToXna() + Vector3.One;
        var size = Vector3.Max(oldSize, newSize);
        if (!oldSize.Equals(size))
        {
            Level.Size = size.ToRepacker();
        }
    }

    #endregion

    #region Instances

    private void UpdateInstanceScale(ToolState.Scale tool, HashSet<InstanceId> selection)
    {
        var scalable = selection.Where(ii => TryGetInstanceScale(ii, out _)).ToList();
        if (scalable.Count == 0)
        {
            return;
        }

        var centroid = ComputeInstanceCentroid(scalable);
        TryGetInstanceScale(scalable[0], out var primaryScale);
        var previousScale = primaryScale;

        if (_gizmo.Scale(centroid, ref primaryScale))
        {
            var delta = primaryScale - previousScale;
            foreach (var id in scalable)
            {
                if (ApplyDeltaToInstanceScale(id, delta))
                {
                    Eddy.Visualize(id);
                }
            }
        }

        if (_gizmo.DragStarted)
        {
            tool.Clear();
            tool.HistoryScope = Eddy.History.BeginScope("Scale Instance(s)");
        }

        if (_gizmo.DragEnded)
        {
            tool.Clear();
        }

        #region Reset an instance scale

        var resettable = scalable.Any(id => id is InstanceId.ArtObject or InstanceId.BackgroundPlane);
        if (resettable)
        {
            Hints.Add("R", "Reset");
        }

        if (resettable && ImGui.IsKeyPressed(ImGuiKey.R) && tool.HistoryScope == null)
        {
            using (Eddy.History.BeginScope("Reset Instance Scale"))
            {
                foreach (var id in scalable)
                {
                    if (ResetInstanceScale(id))
                    {
                        Eddy.Visualize(id);
                    }
                }
            }
        }

        #endregion
    }

    private Vector3 ComputeInstanceCentroid(IReadOnlyCollection<InstanceId> instances)
    {
        return instances
                   .Aggregate(Vector3.Zero, (current, instance) => current + GetInstancePosition(instance))
               / instances.Count;
    }

    private Vector3 GetInstancePosition(InstanceId id)
    {
        return id switch
        {
            InstanceId.ArtObject ao
                when Level.ArtObjects.TryGetValue(ao.Id, out var instance)
                => instance.Position.ToXna(),

            InstanceId.BackgroundPlane bp
                when Level.BackgroundPlanes.TryGetValue(bp.Id, out var instance)
                => instance.Position.ToXna(),

            InstanceId.Volume volume
                when Level.Volumes.TryGetValue(volume.Id, out var instance)
                => (instance.From + instance.To).ToXna() / 2f,

            _ => Vector3.Zero
        };
    }

    private bool TryGetInstanceScale(InstanceId id, out Vector3 scale)
    {
        switch (id)
        {
            case InstanceId.ArtObject ao
                when Level.ArtObjects.TryGetValue(ao.Id, out var instance):
                scale = instance.Scale.ToXna();
                return true;

            case InstanceId.BackgroundPlane bp
                when Level.BackgroundPlanes.TryGetValue(bp.Id, out var instance):
                scale = instance.Scale.ToXna();
                return true;

            case InstanceId.Volume volume
                when Level.Volumes.TryGetValue(volume.Id, out var instance):
                scale = (instance.To - instance.From).ToXna();
                return true;

            default:
                scale = Vector3.Zero;
                return false;
        }
    }

    private bool ApplyDeltaToInstanceScale(InstanceId id, Vector3 delta)
    {
        switch (id)
        {
            case InstanceId.ArtObject ao
                when Level.ArtObjects.TryGetValue(ao.Id, out var instance):
                instance.Scale = (instance.Scale.ToXna() + delta).ToRepacker();
                return true;

            case InstanceId.BackgroundPlane bp
                when Level.BackgroundPlanes.TryGetValue(bp.Id, out var instance):
                instance.Scale = (instance.Scale.ToXna() + delta).ToRepacker();
                return true;

            case InstanceId.Volume volume
                when Level.Volumes.TryGetValue(volume.Id, out var instance):
                var center = (instance.From + instance.To).ToXna() / 2f;
                var size = (instance.To - instance.From).ToXna() + delta;
                instance.From = (center - (size / 2f)).ToRepacker();
                instance.To = (center + (size / 2f)).ToRepacker();
                return true;

            default:
                return false;
        }
    }

    private bool ResetInstanceScale(InstanceId id)
    {
        switch (id)
        {
            case InstanceId.ArtObject ao
                when Level.ArtObjects.TryGetValue(ao.Id, out var instance):
                instance.Scale = RVector3.One;
                return true;

            case InstanceId.BackgroundPlane bp
                when Level.BackgroundPlanes.TryGetValue(bp.Id, out var instance):
                instance.Scale = RVector3.One;
                return true;

            default:
                return false;
        }
    }

    #endregion
}