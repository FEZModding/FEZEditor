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

    private Ray _ray;

    private RaycastHit[] _hits = Array.Empty<RaycastHit>();

    private int _index;

    public RaycastSystem(Scene scene)
    {
        _scene = scene;
    }

    public override void Update()
    {
        if (!Eddy.Frame.AllowsRaycast)
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

        _index = Math.Min(_index, _hits.Length - 1);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.GetIO().KeyAlt)
        {
            _index = (_index + 1) % _hits.Length;
        }

        var hit = ResolveHit();
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

    private (InstanceId Instance, FaceOrientation? Face, TrileEmplacement? Emplacement)? ResolveHit()
    {
        if (Hit is not { } hit)
        {
            return null;
        }

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

            var groups = Level.GetEmplacementGroups();
            InstanceId instance = groups.TryGetValue(emplacement, out var group)
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
                if (Level.Triles.ContainsKey(emplacement))
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