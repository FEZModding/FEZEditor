using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Eddy;

public sealed class CursorSystem : EddySystem
{
    private static readonly Color HoverColor = Color.Blue with { A = 64 }; // 25%

    private static readonly Color SelectionColor = Color.Red with { A = 64 }; // 25%

    private static readonly Color VolumeColor = Color.LimeGreen;

    private static readonly Color PathColor = new(1f, 0.5f, 0f, 0.8f);

    private static readonly MeshSurface UnitBox = MeshSurface.CreateColoredBox(Vector3.One, Color.White);

    private readonly CursorMesh _cursor;

    private int? _hologramTrileId;

    private GroupCursorState? _hoverGroupState;

    private GroupCursorState? _selectionGroupState;

    private int _groupGeometryVersion;

    public CursorSystem(CursorMesh cursor)
    {
        _cursor = cursor;
    }

    public override void Update()
    {
        ClearInstances();
        ClearVolumes();
        ClearPaths();

        if (Eddy.Hovered?.Instance is InstanceId.TrileGroup trileGroup)
        {
            DrawHoveredTrileGroup(trileGroup);
        }
        else
        {
            _hoverGroupState = null;
            _cursor.ClearHover();
            switch (Eddy.Hovered?.Instance)
            {
                case InstanceId.Trile trile:
                    DrawHoveredTrile(trile);
                    break;

                case InstanceId.Volume volume:
                    DrawHoveredVolume(volume);
                    break;

                case InstanceId.PathWaypoint waypoint:
                    DrawHoveredPathWaypoint(waypoint);
                    break;

                case InstanceId.BackgroundPlane bgPlane:
                    DrawHoveredBackgroundPlane(bgPlane);
                    break;

                case { } instance:
                    DrawHoveredInstance(instance);
                    break;
            }
        }

        if (Eddy.Selected is SelectionState.TrileGroup selection)
        {
            DrawSelectedTrileGroup(selection.Selected);
        }
        else
        {
            _selectionGroupState = null;
            _cursor.ClearSelection();
            switch (Eddy.Selected)
            {
                case SelectionState.Trile trile:
                    DrawSelectedTriles(trile.Selected, trile.Face);
                    break;

                case SelectionState.Path path:
                    DrawSelectedPathWaypoints(path.Selected, path.Waypoints);
                    break;

                case SelectionState.Instance instance:
                    DrawSelectedVolumes(instance.Selected.Where(id => id is InstanceId.Volume));
                    DrawSelectedBackgroundPlanes(instance.Selected.Where(id => id is InstanceId.BackgroundPlane).ToList());
                    DrawSelectedInstances(instance.Selected.Where(id => id is not InstanceId.Volume and not InstanceId.BackgroundPlane).ToList());
                    break;
            }
        }

        DrawPaintHologram();
    }

    public override void Visualize(InstanceId instance)
    {
        if (instance is InstanceId.TrileChange or InstanceId.TrileOverlapChange or InstanceId.TrileGroup)
        {
            _groupGeometryVersion++;
        }
    }

    #region Triles

    private void DrawHoveredTrile(InstanceId.Trile trile)
    {
        var instance = Eddy.GetActiveTrile(trile.Emplacement);
        if (instance == null)
        {
            return;
        }

        var face = Eddy.Hovered is { Instance: InstanceId.Trile, Face: var fo } ? fo : FaceOrientation.Top;
        var center = instance.Position.ToXna() + new Vector3(0.5f);
        var origin = center + (face.AsVector() * 0.5f);
        var surface = MeshSurface.CreateFaceQuad(Vector3.One, origin, face);
        _cursor.SetHoverSurfaces([(surface, PrimitiveType.TriangleList)], HoverColor);
    }

    private void DrawSelectedTriles(IReadOnlyCollection<TrileEmplacement> emplacements, FaceOrientation face)
    {
        var normal = face.AsVector();
        var surfaces = emplacements
            .Where(emplacement => Eddy.GetActiveTrile(emplacement) != null)
            .Select(emplacement =>
            {
                var center = Eddy.GetActiveTrile(emplacement)!.Position.ToXna() + new Vector3(0.5f);
                var origin = center + (normal * 0.5f);
                var surface = MeshSurface.CreateFaceQuad(Vector3.One, origin, face);
                return (surface, PrimitiveType.TriangleList);
            });

        _cursor.SetSelectionSurfaces(surfaces, SelectionColor);
    }

    private void DrawPaintHologram()
    {
        if (Eddy.Tool is not ToolState.Paint.Trile tool || Eddy.HoveredTrile is not { } hovered)
        {
            _hologramTrileId = null;
            _cursor.ClearHologram();
            return;
        }

        if (_hologramTrileId != tool.Id)
        {
            _hologramTrileId = tool.Id;
            _cursor.UpdateHologram(Eddy.TrileSet, tool.Id);
        }

        var target = hovered.Trile.Emplacement;
        if (Eddy.OverlapIndex == 0 && ImGui.GetIO().KeyShift)
        {
            target = target.Add(new TrileEmplacement(hovered.Face.AsVector().ToRepacker()));
        }

        if (Eddy.OverlapIndex > 0 && !Level.Triles.ContainsKey(target))
        {
            _cursor.ClearHologram();
            return;
        }

        var position = target.ToXna().ToVector3() + Mathz.EmplacementCenter;
        var phi = (byte)(Eddy.TrilePaintRotationMode switch
        {
            PaintRotationMode.Fixed fixedRotation => fixedRotation.Phi,
            PaintRotationMode.Random randomRotation => randomRotation.LastPhi,
            PaintRotationMode.Copy => GetHoveredPhi(target),
            _ => 0
        });

        _cursor.SetHologramPose(position, Mathz.PhiAngles[phi]);
    }

    private byte GetHoveredPhi(TrileEmplacement emplacement)
    {
        if (!Level.Triles.TryGetValue(emplacement, out var trile))
        {
            return 0;
        }

        if (Eddy.OverlapIndex == 0)
        {
            return trile.PhiLight;
        }

        var slot = Eddy.OverlapIndex - 1;
        return trile.OverlappedTriles != null && slot < trile.OverlappedTriles.Count
            ? trile.OverlappedTriles[slot].PhiLight
            : trile.PhiLight;
    }

    #endregion

    #region Trile Groups

    private void DrawHoveredTrileGroup(InstanceId.TrileGroup trileGroup)
    {
        if (!Level.Groups.TryGetValue(trileGroup.Id, out var group))
        {
            _hoverGroupState = null;
            _cursor.ClearHover();
            return;
        }

        var state = new GroupCursorState(trileGroup, _groupGeometryVersion);
        if (_hoverGroupState == state)
        {
            return;
        }

        _hoverGroupState = state;
        _cursor.ClearHover();
        _cursor.UploadHoverMesh(UnitBox, PrimitiveType.TriangleList, HoverColor);
        _cursor.SetHoverInstances(BuildBoxInstances(group.Triles));
    }

    private void DrawSelectedTrileGroup(IReadOnlyCollection<int> trileGroups)
    {
        var state = new GroupCursorState(trileGroups, _groupGeometryVersion);
        if (_selectionGroupState == state)
        {
            return;
        }

        _selectionGroupState = state;
        _cursor.ClearSelection();
        _cursor.UploadSelectionMesh(UnitBox, PrimitiveType.TriangleList, SelectionColor);
        _cursor.SetSelectionInstances(trileGroups
            .Where(groupId => Level.Groups.ContainsKey(groupId))
            .SelectMany(groupId => Level.Groups[groupId].Triles)
            .Select(CreateBoxInstance));
    }

    private IEnumerable<Matrix> BuildBoxInstances(IEnumerable<TrileInstance> triles)
    {
        return triles
            .Select(trile => new TrileEmplacement(trile.Position))
            .Where(Level.Triles.ContainsKey)
            .Select(emplacement => CreateBoxInstance(Level.Triles[emplacement]));
    }

    private static Matrix CreateBoxInstance(TrileInstance trile)
    {
        var center = trile.Position.ToXna() + new Vector3(0.5f);
        return new Matrix(
            center.X, center.Y, center.Z, 0f,
            0f, 0f, 0f, 1f,
            1f, 1f, 1f, 0f,
            0f, 0f, 0f, 0f
        );
    }

    #endregion

    #region Volumes

    private void DrawHoveredVolume(InstanceId.Volume volume)
    {
        if (Eddy.Registry.TryGetActor(volume, out var actor))
        {
            var volumeMesh = actor.GetComponent<VolumeMesh>();
            volumeMesh.Color = HoverColor;
        }
    }

    private void DrawSelectedVolumes(IEnumerable<InstanceId> volumes)
    {
        foreach (var volume in volumes)
        {
            if (Eddy.Registry.TryGetActor(volume, out var actor))
            {
                var volumeMesh = actor.GetComponent<VolumeMesh>();
                volumeMesh.Color = SelectionColor;
            }
        }
    }

    private void ClearVolumes()
    {
        foreach (var actor in Eddy.Registry.GetActors<InstanceId.Volume>())
        {
            var volumeMesh = actor.GetComponent<VolumeMesh>();
            volumeMesh.Color = VolumeColor;
        }
    }

    #endregion

    #region Paths

    private void DrawHoveredPathWaypoint(InstanceId.PathWaypoint waypoint)
    {
        SetPathWaypointColor(waypoint.PathId, HoverColor, waypoint.Index);
    }

    private void DrawSelectedPathWaypoints(InstanceId instance, HashSet<int> waypoints)
    {
        if (instance is InstanceId.Path or InstanceId.GroupPath)
        {
            SetPathWaypointColor(instance, SelectionColor, waypoints);
        }
    }

    private void SetPathWaypointColor(InstanceId instance, Color color, params HashSet<int> indexes)
    {
        if (Eddy.Registry.TryGetActor(instance, out var actor))
        {
            var pathMesh = actor.GetComponent<PathMesh>();
            for (var i = 0; i < pathMesh.WaypointColors.Count; i++)
            {
                if (indexes.Contains(i))
                {
                    pathMesh.WaypointColors[i] = color;
                }
            }
        }
    }

    private void ClearPaths()
    {
        var levelPaths = Eddy.Registry.GetActors<InstanceId.Path>();
        var groupPaths = Eddy.Registry.GetActors<InstanceId.GroupPath>();
        foreach (var actor in levelPaths.Concat(groupPaths))
        {
            var pathMesh = actor.GetComponent<PathMesh>();
            for (var i = 0; i < pathMesh.WaypointColors.Count; i++)
            {
                pathMesh.WaypointColors[i] = PathColor;
            }
        }
    }

    #endregion

    #region Background Planes

    private void DrawHoveredBackgroundPlane(InstanceId.BackgroundPlane instance)
    {
        var hoverSurfaces = new[] {
            BuildWireframe(instance, HoverColor),
            BuildBackgroundPlaneBackQuad(instance)
        }.Where(s => s.HasValue).Select(s => s!.Value).ToList();
        if (hoverSurfaces.Count > 0)
        {
            _cursor.SetHoverSurfaces(hoverSurfaces, HoverColor);
        }

        TintInstances([instance], HoverColor);
    }

    private void DrawSelectedBackgroundPlanes(IEnumerable<InstanceId> instances)
    {
        var planeInstances = instances.OfType<InstanceId.BackgroundPlane>().ToList();

        var selectionSurfaces = planeInstances
            .SelectMany(id => new[] { BuildWireframe(id, SelectionColor), BuildBackgroundPlaneBackQuad(id) })
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .ToList();

        if (selectionSurfaces.Count > 0)
        {
            _cursor.SetSelectionSurfaces(selectionSurfaces, SelectionColor);
        }

        TintInstances(planeInstances, SelectionColor);
    }

    private (MeshSurface, PrimitiveType)? BuildBackgroundPlaneBackQuad(InstanceId instance)
    {
        if (!Eddy.Registry.TryGetActor(instance, out var actor) ||
            !actor.TryGetComponent<BackgroundPlaneMesh>(out var mesh))
        {
            return null;
        }

        if (mesh!.DoubleSided)
        {
            return null;
        }

        var surface = MeshSurface.CreateQuad(mesh.PlaneSize * actor.Transform.Scale);
        var rotationMatrix = Matrix.CreateFromQuaternion(actor.Transform.Rotation);
        for (var i = 0; i < surface.Vertices.Length; i++)
        {
            surface.Vertices[i] = Vector3.Transform(surface.Vertices[i], rotationMatrix) + actor.Transform.Position;
        }

        return (surface, PrimitiveType.TriangleList);
    }

    #endregion

    #region Instances

    private void DrawHoveredInstance(InstanceId instance)
    {
        var hoverSurfaces = BuildWireframe(instance, HoverColor);
        if (hoverSurfaces.HasValue)
        {
            _cursor.SetHoverSurfaces([hoverSurfaces.Value], HoverColor);
        }

        TintInstances([instance], HoverColor);
    }

    private void DrawSelectedInstances(IReadOnlyList<InstanceId> instances)
    {
        var selectionSurfaces = instances
            .Select(id => BuildWireframe(id, SelectionColor))
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .ToList();

        if (selectionSurfaces.Count > 0)
        {
            _cursor.SetSelectionSurfaces(selectionSurfaces, SelectionColor);
        }

        TintInstances(instances, SelectionColor);
    }

    private void ClearInstances()
    {
        TintInstances(Eddy.Registry.Instances, Color.Transparent);
    }

    private void TintInstances(IEnumerable<InstanceId> instances, Color color)
    {
        foreach (var instance in instances)
        {
            if (Eddy.Registry.TryGetActor(instance, out var actor))
            {
                if (actor.TryGetComponent<ITinted>(out var tinted))
                {
                    tinted?.Tint = color;
                }
            }
        }
    }

    private (MeshSurface, PrimitiveType)? BuildWireframe(InstanceId instance, Color color)
    {
        if (!Eddy.Registry.TryGetActor(instance, out var actor))
        {
            return null;
        }

        if (!actor.TryGetComponent<IPickable>(out var pickable) || pickable == null)
        {
            return null;
        }

        var box = pickable.GetBounds().FirstOrDefault();
        var size = box.Max - box.Min;
        var center = (box.Min + box.Max) * 0.5f;

        var surface = MeshSurface.CreateWireframeBox(size, color);
        for (var i = 0; i < surface.Vertices.Length; i++)
        {
            surface.Vertices[i] += center;
        }

        return (surface, PrimitiveType.LineList);
    }

    #endregion

    private sealed record GroupCursorState(object Selection, int GeometryVersion);
}