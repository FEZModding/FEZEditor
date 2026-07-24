using FezEditor.Actors;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public class RotateToolSystem : EddySystem
{
    private readonly Gizmo _gizmo;

    public RotateToolSystem(Gizmo gizmo)
    {
        _gizmo = gizmo;
    }

    public override void Update()
    {
        if (Eddy.Tool is not ToolState.Rotate)
        {
            return;
        }

        var gizmoCapturedMouse = false;
        switch (Eddy.Selected)
        {
            case SelectionState.Trile triles:
                gizmoCapturedMouse = UpdateTrileRotate(triles);
                break;

            case SelectionState.TrileGroup trileGroup:
                gizmoCapturedMouse = UpdateTrileGroupRotate(trileGroup);
                break;

            case SelectionState.Instance instances:
                gizmoCapturedMouse = UpdateInstanceRotate(instances.Selected);
                break;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Eddy.Tool = new ToolState.Select();
            return;
        }

        // Only an unhandled click on empty viewport space clears the selection
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
            !ImGui.GetIO().KeyShift &&
            Eddy.Frame.AllowsSelection &&
            Eddy.Hovered is null &&
            !_gizmo.IsActive &&
            !gizmoCapturedMouse)
        {
            Eddy.Selected = new SelectionState.Empty();
            Eddy.Tool = new ToolState.Select();
        }
    }

    public override bool IsToolEnabled(ToolState tool)
    {
        return tool is ToolState.Rotate && Eddy.Selected switch
        {
            SelectionState.Trile { Selected.Count: > 0 } => true,
            SelectionState.TrileGroup { Selected.Count: > 0 } => true,
            SelectionState.Instance i => i.Selected.Any(id => id is InstanceId.ArtObject or
                InstanceId.BackgroundPlane or
                InstanceId.Gomez),
            _ => false
        };
    }

    #region Triles and Trile Groups

    private bool UpdateTrileGroupRotate(SelectionState.TrileGroup selection)
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
            return false;
        }

        return UpdateTrileRotate(new SelectionState.Trile(emplacements, FaceOrientation.Top, emplacements[0]));
    }

    private bool UpdateTrileRotate(SelectionState.Trile selection)
    {
        if (selection.Selected.Count == 0)
        {
            return false;
        }

        var centroid = ComputeTrileCentroid(selection);
        var gizmoCapturedMouse = _gizmo.Rotate(centroid);
        if (gizmoCapturedMouse)
        {
            using (Eddy.History.BeginScope("Rotate Trile(s)"))
            {
                foreach (var emplacement in selection.Selected)
                {
                    RotateTrile(emplacement);
                }

                Eddy.Visualize(new InstanceId.CollisionMap());
                Eddy.Visualize(new InstanceId.PickableBounds());
            }
        }

        return gizmoCapturedMouse;
    }

    private Vector3 ComputeTrileCentroid(SelectionState.Trile selection)
    {
        var instances = selection.Selected
            .Select(Eddy.GetActiveTrile)
            .Where(ti => ti != null)
            .ToList();

        return instances.Count == 0
            ? Vector3.Zero
            : instances.Aggregate(Vector3.Zero, (sum, ti) => sum + ti!.Position.ToXna()) / instances.Count;
    }

    private void RotateTrile(TrileEmplacement emplacement)
    {
        var active = Eddy.GetActiveTrile(emplacement);
        if (active == null)
        {
            return;
        }

        var before = active.Clone();
        active.PhiLight = (byte)((active.PhiLight + 1) % 4);
        var after = active.Clone();

        InstanceId instance = Eddy.OverlapIndex <= 0
            ? new InstanceId.TrileChange(emplacement, before, after)
            : new InstanceId.TrileOverlapChange(emplacement, Eddy.OverlapIndex - 1, before, after);

        Eddy.Visualize(instance);
    }

    #endregion

    #region Instances

    private bool UpdateInstanceRotate(HashSet<InstanceId> selection)
    {
        var rotatable = selection.Where(ii => TryGetInstancePosition(ii, out _)).ToList();
        if (rotatable.Count == 0)
        {
            return false;
        }

        var centroid = ComputeInstanceCentroid(rotatable);
        var gizmoCapturedMouse = _gizmo.Rotate(centroid);
        if (gizmoCapturedMouse)
        {
            using (Eddy.History.BeginScope("Rotate Instance(s)"))
            {
                foreach (var id in rotatable)
                {
                    if (RotateInstance(id))
                    {
                        Eddy.Visualize(id);
                    }
                }
            }
        }

        return gizmoCapturedMouse;
    }

    private Vector3 ComputeInstanceCentroid(IReadOnlyCollection<InstanceId> instances)
    {
        var sum = Vector3.Zero;
        foreach (var instance in instances)
        {
            TryGetInstancePosition(instance, out var position);
            sum += position;
        }

        return sum / instances.Count;
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

            case InstanceId.Gomez when Level.StartingFace != null:
                position = Level.StartingFace.Id.AsVector().ToXna() + Vector3.Up;
                return true;

            default:
                position = Vector3.Zero;
                return false;
        }
    }

    private bool RotateInstance(InstanceId id)
    {
        var step = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.PiOver2);
        switch (id)
        {
            case InstanceId.ArtObject ao
                when Level.ArtObjects.TryGetValue(ao.Id, out var instance):
                instance.Rotation = (step * instance.Rotation.ToXna()).ToRepacker();
                return true;

            case InstanceId.BackgroundPlane bp
                when Level.BackgroundPlanes.TryGetValue(bp.Id, out var instance):
                instance.Rotation = (step * instance.Rotation.ToXna()).ToRepacker();
                return true;

            case InstanceId.Gomez when Level.StartingFace != null:
                var index = Array.IndexOf(FaceExtensions.NaturalOrder, Level.StartingFace.Face);
                Level.StartingFace.Face = FaceExtensions.NaturalOrder[(index + 1) % 4];
                return true;

            default:
                return false;
        }
    }

    #endregion
}