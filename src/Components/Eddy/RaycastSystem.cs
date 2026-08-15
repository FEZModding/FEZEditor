using FezEditor.Actors;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public class RaycastSystem : EddySystem
{
    private RaycastHit? Hit => _hits.Length == 0 ? null : _hits[_index];

    private readonly Scene _scene;

    private readonly Gizmo _gizmo;

    private Ray _ray;

    private RaycastHit[] _hits = Array.Empty<RaycastHit>();

    private int _index;

    public RaycastSystem(Scene scene, Gizmo gizmo)
    {
        _scene = scene;
        _gizmo = gizmo;
    }

    public override void Update()
    {
        var isTransformTool = Eddy.Tool is ToolState.Translate or ToolState.Rotate or ToolState.Scale;
        if (!Eddy.Frame.AllowsRaycast || (_gizmo.IsActive && isTransformTool))
        {
            Eddy.Hovered = null;
            Eddy.HoveredTrile = null;
            return;
        }

        _ray = _scene.Viewport.Unproject(ImGuiX.GetMousePos(), Eddy.Frame.Position);
        _hits = _scene.RaycastAll(_ray);
        if (_hits.Length < 1)
        {
            Eddy.Hovered = null;
            Eddy.HoveredTrile = null;
            return;
        }

        _index = 0;
        var first = ResolveHit(0);
        if (ImGui.GetIO().KeyAlt && first?.Instance is not InstanceId.Trile and not InstanceId.TrileGroup)
        {
            var selectedIndex = FindSelectionCycleIndex();
            if (selectedIndex >= 0)
            {
                _index = FindNextSelectionCycleIndex(selectedIndex);
            }
        }

        var hit = ResolveHit(_index);
        Eddy.Hovered = hit.HasValue ? (hit.Value.Instance, hit.Value.Face ?? FaceOrientation.Top) : null;
        Eddy.HoveredTrile = ResolveHoveredTrile();
    }

    public override void Draw()
    {
        if (!Eddy.ShowRaycastDebug)
        {
            return;
        }

        var stats = new Dictionary<string, string>
        {
            ["Hovered"] = Eddy.Hovered?.ToString() ?? "None",
            ["Selected"] = Eddy.Selected.ToString()
        };

        if (Hit is var (actor, distance, index))
        {
            stats["Hit"] = $"{actor.Name} ({index + 1}/{_hits.Length})";
            stats["Distance"] = $"{distance:F2}";
            stats["Triangle"] = $"{index}";
            if (actor.TryGetComponent<TrilesMesh>(out var mesh) && mesh != null)
            {
                var emp = mesh.GetEmplacement(index);
                stats["Emplacement"] = $"{emp.X}, {emp.Y}, {emp.Z}";
            }
        }
        else
        {
            stats["Hit"] = "None";
        }

        var (_, height) = _scene.Viewport.GetSize();
        var lineHeight = ImGui.GetTextLineHeight();
        var position = Eddy.Frame.Position + new Vector2(8, height - 8f);
        ImGuiX.DrawStats(position - new Vector2(0, (lineHeight * stats.Count) + 8), stats);
    }

    private int FindSelectionCycleIndex()
    {
        for (var i = 0; i < _hits.Length; i++)
        {
            var hit = ResolveHit(i);
            if (hit is { Instance: not InstanceId.Trile and not InstanceId.TrileGroup } &&
                IsInstanceSelected(hit.Value.Instance))
            {
                return i;
            }
        }

        return -1;
    }

    private int FindNextSelectionCycleIndex(int selectedIndex)
    {
        for (var offset = 1; offset < _hits.Length; offset++)
        {
            var index = (selectedIndex + offset) % _hits.Length;
            if (ResolveHit(index)?.Instance is not null and not InstanceId.Trile and not InstanceId.TrileGroup)
            {
                return index;
            }
        }

        return selectedIndex;
    }

    private bool IsInstanceSelected(InstanceId instance)
    {
        return (instance, Eddy.Selected) switch
        {
            (InstanceId.Trile t, SelectionState.Trile s) => s.Selected.Contains(t.Emplacement),
            (InstanceId.TrileGroup g, SelectionState.TrileGroup s) => s.Selected.Contains(g.Id),
            (InstanceId id, SelectionState.Instance s) => s.Selected.Contains(id),
            (InstanceId.PathWaypoint wp, SelectionState.Path s) =>
                wp.PathId == s.Selected && s.Waypoints.Contains(wp.Index),
            _ => false
        };
    }

    private (InstanceId Instance, FaceOrientation? Face, TrileEmplacement? Emplacement)? ResolveHit(int index)
    {
        if (index >= _hits.Length)
        {
            return null;
        }

        var hit = _hits[index];

        if (hit.Actor.TryGetComponent<TrilesMesh>(out var trilesMesh) && trilesMesh != null)
        {
            if (hit.Index >= trilesMesh.InstanceCount)
            {
                return null;
            }

            var emplacement = trilesMesh.GetEmplacement(hit.Index);
            if (!Level.Triles.ContainsKey(emplacement))
            {
                return null;
            }

            var bounds = trilesMesh.GetBounds().ElementAt(hit.Index);
            var face = Mathz.DetermineFace(bounds, _ray, hit.Distance);

            if (Eddy.GetActiveTrile(emplacement) == null)
            {
                return null;
            }

            var groups = Level.GetEmplacementGroups();
            InstanceId instance = Eddy.OverlapIndex == 0 && groups.TryGetValue(emplacement, out var group)
                ? new InstanceId.TrileGroup(group)
                : new InstanceId.Trile(emplacement);

            return (instance, face, emplacement);
        }

        if (hit.Actor.TryGetComponent<PathMesh>(out var pathMesh) && pathMesh != null)
        {
            if (hit.Index >= pathMesh.Waypoints.Count)
            {
                return null;
            }

            if (!Eddy.Registry.TryGetInstance(hit.Actor, out var instance))
            {
                return null;
            }

            return (new InstanceId.PathWaypoint(instance, hit.Index), null, null);
        }

        if (Eddy.Registry.TryGetInstance(hit.Actor, out var id))
        {
            return (id, null, null);
        }

        return null;
    }

    private (InstanceId.Trile Trile, FaceOrientation Face)? ResolveHoveredTrile()
    {
        foreach (var hit in _hits)
        {
            if (hit.Actor.TryGetComponent<TrilesMesh>(out var trilesMesh) &&
                trilesMesh != null &&
                hit.Index < trilesMesh.InstanceCount)
            {
                var emplacement = trilesMesh.GetEmplacement(hit.Index);
                if (Level.Triles.TryGetValue(emplacement, out var trile) &&
                    trile.TrileId != EddyEditor.InvalidId)
                {
                    var bounds = trilesMesh.GetBounds().ElementAt(hit.Index);
                    var face = Mathz.DetermineFace(bounds, _ray, hit.Distance);
                    return (new InstanceId.Trile(emplacement), face);
                }
            }
        }

        return null;
    }
}