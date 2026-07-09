using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;

namespace FezEditor.Components.Eddy;

public class SelectionToolSystem : EddySystem
{
    public override void Update()
    {
        if (Eddy.Tool is not ToolState.Select state)
        {
            return;
        }

        #region Display hints

        if (Eddy.Frame.AllowsSelection)
        {
            Status.AddHint("LMB", "Select");
            Status.AddHint("Shift+LMB", "Add to Selection");

            if (Eddy.Hovered?.Instance is not InstanceId.Trile and not InstanceId.TrileGroup)
            {
                Status.AddHint("Alt+LMB", "Cycle Selection");
            }
        }

        if (Eddy.Hovered?.Instance is InstanceId.Trile)
        {
            Status.AddHint("LMB Drag", "Select Multiple");
        }

        #endregion

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Eddy.Selected = new SelectionState.Empty();
            return;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.GetIO().KeyAlt)
        {
            if (Eddy.Hovered is { } h && Eddy.Frame.AllowsSelection)
            {
                SelectCandidate(h.Instance, h.Face, false);
            }
            return;
        }

        #region Selection on single click (also start rect selection on triles)

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.GetIO().KeyAlt)
        {
            if (Eddy.Hovered?.Instance is InstanceId.Trile trile)
            {
                state.DragOrigin = trile;
                state.IsRectSelecting = false;
                return;
            }

            var additive = ImGui.GetIO().KeyShift;
            if (Eddy.Hovered is not { } h)
            {
                // Only click on empty space inside a viewport is valid
                if (!additive && Eddy.Frame.AllowsSelection)
                {
                    Eddy.Selected = new SelectionState.Empty();
                }

                return;
            }

            SelectCandidate(h.Instance, h.Face, additive);
        }

        #endregion

        #region Continue rect selection while dragging

        if (ImGui.IsMouseDragging(ImGuiMouseButton.Left) &&
            state.DragOrigin is { } origin &&
            Eddy.Hovered is { Instance: InstanceId.Trile current, Face: var face })
        {
            state.IsRectSelecting = true;
            SelectTrileRect(origin.Emplacement, current.Emplacement, face);
            return;
        }

        #endregion

        #region Finish rect selection

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) && state.DragOrigin is not null)
        {
            var wasBoxSelecting = state.IsRectSelecting;
            state.DragOrigin = null;
            state.IsRectSelecting = false;

            if (!wasBoxSelecting && Eddy.Hovered is { Instance: var releasedHit, Face: var face1 })
            {
                SelectCandidate(releasedHit, face1, ImGui.GetIO().KeyShift);
            }
        }

        #endregion
    }

    private void SelectCandidate(InstanceId instance, FaceOrientation? face, bool additive)
    {
        switch (instance)
        {
            case InstanceId.Trile trile:
                SelectEmplacement(trile, face, additive);
                break;

            case InstanceId.TrileGroup trileGroup:
                SelectGroup(trileGroup, additive);
                break;

            case InstanceId.PathWaypoint pathWaypoint:
                SelectPath(pathWaypoint, additive);
                break;

            default:
                SelectInstance(instance, additive);
                break;
        }
    }

    private void SelectEmplacement(InstanceId.Trile trile, FaceOrientation? face, bool additive)
    {
        var current = Eddy.Selected is SelectionState.Trile state ? state.Selected : [];
        var selected = additive ? new List<TrileEmplacement>(current) : [];

        if (!selected.Contains(trile.Emplacement))
        {
            selected.Add(trile.Emplacement);
        }
        else
        {
            selected.Remove(trile.Emplacement);
        }

        Eddy.Selected = selected.Count == 0
            ? new SelectionState.Empty()
            : new SelectionState.Trile(selected, face ?? FaceOrientation.Front, trile.Emplacement);
    }

    private void SelectGroup(InstanceId.TrileGroup group, bool additive)
    {
        var current = Eddy.Selected is SelectionState.TrileGroup state ? state.Selected : [];
        var selected = Select(current, group.Id, additive);

        Eddy.Selected = selected.Count == 0
            ? new SelectionState.Empty()
            : new SelectionState.TrileGroup(selected);
    }

    private void SelectPath(InstanceId.PathWaypoint pathWaypoint, bool additive)
    {
        var current = Eddy.Selected is SelectionState.Path state && state.Selected == pathWaypoint.PathId
            ? state.Waypoints
            : [];
        var selected = Select(current, pathWaypoint.Index, additive);

        Eddy.Selected = selected.Count == 0
            ? new SelectionState.Empty()
            : new SelectionState.Path(pathWaypoint.PathId, selected);
    }

    private void SelectInstance(InstanceId id, bool additive)
    {
        var current = Eddy.Selected is SelectionState.Instance state ? state.Selected : [];
        var selected = Select(current, id, additive);

        Eddy.Selected = selected.Count == 0
            ? new SelectionState.Empty()
            : new SelectionState.Instance(selected);
    }

    private static HashSet<T> Select<T>(IEnumerable<T> current, T item, bool additive)
    {
        var selected = additive ? new HashSet<T>(current) : [];

        if (!selected.Add(item))
        {
            selected.Remove(item);
        }

        return selected;
    }

    private void SelectTrileRect(
        TrileEmplacement origin,
        TrileEmplacement current,
        FaceOrientation? face)
    {
        var min = origin.Min(current);
        var max = origin.Max(current);
        var selected = new List<TrileEmplacement>();

        for (var x = min.X; x <= max.X; x++)
        {
            for (var y = min.Y; y <= max.Y; y++)
            {
                for (var z = min.Z; z <= max.Z; z++)
                {
                    var emp = new TrileEmplacement(x, y, z);
                    if (Eddy.GetActiveTrile(emp) == null)
                    {
                        continue;
                    }

                    var siblings = Eddy.OverlapIndex == 0
                        ? Level.GetGroupSiblingEmplacements(emp)
                        : [];

                    if (siblings.Count == 0)
                    {
                        if (!selected.Contains(emp))
                        {
                            selected.Add(emp);
                        }

                        continue;
                    }

                    foreach (var sibling in siblings)
                    {
                        if (!selected.Contains(sibling))
                        {
                            selected.Add(sibling);
                        }
                    }
                }
            }
        }

        Eddy.Selected = selected.Count == 0
            ? new SelectionState.Empty()
            : new SelectionState.Trile(selected, face ?? FaceOrientation.Front, origin);
    }
}