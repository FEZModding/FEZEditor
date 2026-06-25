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
    private static readonly Color HoverColor = Color.Blue with { A = 85 };

    private static readonly Color SelectionColor = Color.Red with { A = 85 };

    private static readonly Color VolumeColor = Color.LimeGreen;

    private static readonly Color PathColor = new(1f, 0.5f, 0f, 0.8f);

    private readonly CursorMesh _cursor;

    private int? _hologramTrileId;

    public CursorSystem(CursorMesh cursor)
    {
        _cursor = cursor;
    }

    public override void Update()
    {
        ClearInstances();
        ClearVolumes();
        ClearPaths();

        _cursor.ClearHover();
        switch (Eddy.Hovered?.Instance)
        {
            case InstanceId.Trile trile:
                DrawHoveredTrile(trile);
                break;

            case InstanceId.TrileGroup trileGroup:
                DrawHoveredTrileGroup(trileGroup);
                break;

            case InstanceId.Volume volume:
                DrawHoveredVolume(volume);
                break;

            case InstanceId.PathWaypoint waypoint:
                DrawHoveredPathWaypoint(waypoint);
                break;

            case { } instance:
                DrawHoveredInstance(instance);
                break;
        }

        _cursor.ClearSelection();
        switch (Eddy.Selected)
        {
            case SelectionState.Trile trile:
                DrawSelectedTriles(trile.Selected, trile.Face);
                break;

            case SelectionState.TrileGroup trileGroup:
                DrawSelectedTrileGroup(trileGroup.Selected);
                break;

            case SelectionState.Path path:
                DrawSelectedPathWaypoints(path.Selected, path.Waypoints);
                break;

            case SelectionState.Instance instance:
                DrawSelectedVolumes(instance.Selected.Where(id => id is InstanceId.Volume));
                DrawSelectedInstances(instance.Selected.Where(id => id is not InstanceId.Volume).ToList());
                break;
        }

        DrawPaintHologram();
    }

    #region Triles

    private void DrawHoveredTrile(InstanceId.Trile trile)
    {
        if (!Level.Triles.TryGetValue(trile.Emplacement, out var instance))
        {
            return;
        }

        var face = Eddy.Hovered is { Instance: InstanceId.Trile, Face: var fo } ? fo : FaceOrientation.Top;
        var center = instance.Position.ToXna() + new Vector3(0.5f);
        var origin = center + (face.AsVector() * (0.5f + CursorMesh.OverlayOffset));
        var surface = MeshSurface.CreateFaceQuad(Vector3.One, origin, face);
        _cursor.SetHoverSurfaces([(surface, PrimitiveType.TriangleList)], HoverColor);
    }

    private void DrawSelectedTriles(IReadOnlyCollection<TrileEmplacement> emplacements, FaceOrientation face)
    {
        var normal = face.AsVector();
        var surfaces = emplacements
            .Where(emplacement => Level.Triles.ContainsKey(emplacement))
            .Select(emplacement =>
            {
                var center = Level.Triles[emplacement].Position.ToXna() + new Vector3(0.5f);
                var origin = center + (normal * (0.5f + CursorMesh.OverlayOffset));
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

        var position = hovered.Trile.Emplacement.ToXna().ToVector3() + TrilesMesh.EmplacementCenter;
        if (ImGui.GetIO().KeyShift)
        {
            position += hovered.Face.AsVector();
        }

        var phi = (byte)(tool.RotationMode switch
        {
            PaintRotationMode.Fixed fixedRotation => fixedRotation.Phi,
            PaintRotationMode.Random randomRotation => randomRotation.LastPhi,
            PaintRotationMode.Copy => GetHoveredPhi(hovered.Trile.Emplacement),
            _ => 0
        });

        _cursor.SetHologramPose(position, TrilesMesh.PhiAngles[phi]);
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
            return;
        }

        var groupSet = group.Triles.Select(ti => new TrileEmplacement(ti.Position));
        _cursor.SetHoverSurfaces(BuildBoxSurfaces(groupSet, HoverColor), HoverColor);
    }

    private void DrawSelectedTrileGroup(IReadOnlyCollection<int> trileGroups)
    {
        var surfaces = trileGroups
            .Where(groupId => Level.Groups.ContainsKey(groupId))
            .Select(groupId =>
            {
                var emplacements = Level.Groups[groupId].Triles.Select(ti => new TrileEmplacement(ti.Position));
                return BuildBoxSurfaces(emplacements, SelectionColor);
            })
            .SelectMany(x => x);

        _cursor.SetSelectionSurfaces(surfaces, SelectionColor);
    }

    private IEnumerable<(MeshSurface, PrimitiveType)> BuildBoxSurfaces(
        IEnumerable<TrileEmplacement> emplacements,
        Color color)
    {
        return emplacements
            .Where(e => Level.Triles.TryGetValue(e, out _))
            .Select(e =>
            {
                var center = Level.Triles[e].Position.ToXna() + new Vector3(0.5f);
                var surface = MeshSurface.CreateColoredBox(Vector3.One, color);
                for (var i = 0; i < surface.Vertices.Length; i++)
                {
                    surface.Vertices[i] += center;
                }

                return (surface, PrimitiveType.TriangleList);
            });
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
}