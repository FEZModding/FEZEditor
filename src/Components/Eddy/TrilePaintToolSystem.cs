using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public class TrilePaintToolSystem : EddySystem
{
    public override void Update()
    {
        if (Eddy.Tool is not ToolState.Paint.Trile tool)
        {
            return;
        }

        #region Update rotation of trile

        if (Eddy.TrilePaintRotationMode is PaintRotationMode.Fixed @fixed &&
            Input.CaptureScrollWheelDelta(out var scroll))
        {
            var delta = scroll > 0 ? 1 : -1;
            var phi = (byte)((@fixed.Phi + delta + 4) % 4);
            Eddy.TrilePaintRotationMode = new PaintRotationMode.Fixed(phi);
        }

        if (ImGui.IsKeyPressed(ImGuiKey.R))
        {
            Eddy.TrilePaintRotationMode = Eddy.TrilePaintRotationMode switch
            {
                PaintRotationMode.Fixed fixedRotation => new PaintRotationMode.Random(fixedRotation.Phi),
                PaintRotationMode.Random => new PaintRotationMode.Copy(),
                PaintRotationMode.Copy => new PaintRotationMode.Fixed(GetHoveredPhi()),
                _ => new PaintRotationMode.Fixed(0)
            };
        }

        #endregion

        #region Update status hits

        if (Eddy.OverlapIndex > 0)
        {
            Hints.Add("LMB", $"Paint Layer {Eddy.OverlapIndex}");
            Hints.Add("Ctrl+LMB", $"Erase Layer {Eddy.OverlapIndex} on Release");
        }
        else
        {
            Hints.Add("LMB", "Paint");
            Hints.Add("Shift+LMB", "Append");
            Hints.Add("Ctrl+LMB", "Erase on Release");
        }

        Hints.Add("R", $"Rotate: {Eddy.TrilePaintRotationMode.DisplayName}");

        #endregion

        #region Handle inputs

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Eddy.Tool = new ToolState.Select();
            return;
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            var eraseStroke = tool.EraseStroke;
            tool.Clear();
            if (eraseStroke is { Targets.Count: > 0 } stroke && stroke.OverlapIndex == Eddy.OverlapIndex)
            {
                EraseTriles(stroke.Targets, stroke.OverlapIndex);
            }

            return;
        }

        var clicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        var inputFrame = ImGui.GetFrameCount();
        if (tool.EraseStroke is { } activeStroke &&
            (!ImGui.IsMouseDown(ImGuiMouseButton.Left) || (clicked && activeStroke.StartFrame != inputFrame)))
        {
            tool.Clear();
        }

        if (tool.EraseStroke != null)
        {
            return;
        }

        if (ImGui.GetIO().KeyCtrl && (Eddy.OverlapIndex > 0 || !ImGui.GetIO().KeyShift))
        {
            if (clicked && Eddy.Frame.AllowsRaycast &&
                Eddy.HoveredTrile is { } eraseTarget &&
                Eddy.GetActiveTrile(eraseTarget.Trile.Emplacement) != null)
            {
                tool.Clear();
                var origin = eraseTarget.Trile.Emplacement;
                tool.EraseStroke = (Eddy.OverlapIndex, origin, [origin], inputFrame);
            }

            return;
        }

        if (Eddy.HoveredTrile is not { } hovered ||
            (!clicked && !ImGui.IsMouseDragging(ImGuiMouseButton.Left)))
        {
            return;
        }

        #endregion

        #region Resolve and append parking spot

        if (!ImGui.GetIO().KeyShift)
        {
            tool.ParkingSpot = null;
        }
        else if (tool.ParkingSpot is { } ps && hovered.Trile.Emplacement.Equals(ps.Position))
        {
            var resolved = (new InstanceId.Trile(ps.Anchor), ps.Face);
            Eddy.HoveredTrile = resolved;

            if (Eddy.Hovered is { Instance: InstanceId.Trile trile } && trile.Emplacement.Equals(ps.Position))
            {
                Eddy.Hovered = resolved;
            }

            hovered = resolved;
        }

        #endregion

        #region Begin paint stroke

        tool.HistoryScope ??= Eddy.History.BeginScope("Paint Triles");

        #endregion

        #region Process paint stroke

        if (Eddy.Selected is not SelectionState.Trile selection ||
            !selection.Selected.Contains(hovered.Trile.Emplacement))
        {
            PaintTriles(tool, hovered.Face, [hovered.Trile.Emplacement]);
        }
        else if (selection.Selected.Count > 0)
        {
            PaintTriles(tool, selection.Face, selection.Selected.ToArray());
        }

        #endregion
    }

    private void EraseTriles(IEnumerable<TrileEmplacement> targets, int overlapIndex)
    {
        using var scope = Eddy.History.BeginScope("Erase Triles");
        foreach (var emplacement in targets.Where(emplacement => Eddy.GetActiveTrile(emplacement) != null))
        {
            if (overlapIndex > 0)
            {
                EraseOverlap(emplacement, overlapIndex - 1);
            }
            else
            {
                EraseTrile(emplacement);
            }
        }
    }

    private void PaintTriles(
        ToolState.Paint.Trile tool,
        FaceOrientation face,
        IEnumerable<TrileEmplacement> emplacements)
    {
        foreach (var emplacement in emplacements)
        {
            if (Eddy.OverlapIndex > 0)
            {
                PaintOverlap(tool, emplacement, Eddy.OverlapIndex - 1);
            }
            else if (ImGui.GetIO().KeyShift)
            {
                AppendTrile(tool, emplacement, face);
            }
            else
            {
                PaintTrile(tool, emplacement);
            }
        }
    }

    private void PaintTrile(ToolState.Paint.Trile tool, TrileEmplacement emplacement)
    {
        if (!Level.Triles.TryGetValue(emplacement, out var instance))
        {
            return;
        }

        var phi = ResolvePhi(instance);
        if (instance.TrileId == tool.Id && instance.PhiLight == phi)
        {
            return;
        }

        if (!tool.Stroke.Add(new InstanceId.Trile(emplacement)))
        {
            return;
        }

        var before = instance.Clone();
        instance.TrileId = tool.Id;
        instance.PhiLight = phi;
        var after = instance.Clone();
        SyncTrile(emplacement, before, after);
    }

    private void AppendTrile(ToolState.Paint.Trile tool, TrileEmplacement source, FaceOrientation face)
    {
        var step = face.AsVector().ToRepacker();
        var target = source.Add(new TrileEmplacement(step));

        if (!Level.Triles.TryGetValue(source, out var sourceTrile))
        {
            return;
        }

        if (!tool.Stroke.Add(new InstanceId.Trile(target)))
        {
            return;
        }

        var phi = ResolvePhi(sourceTrile);
        var instance = new TrileInstance
        {
            TrileId = tool.Id,
            PhiLight = phi,
            Position = new Vector3(target.X, target.Y, target.Z).ToRepacker()
        };

        var before = Level.Triles.TryGetValue(target, out var existing) ? existing.Clone() : null;
        Level.Triles[target] = instance;
        SyncTrile(target, before, instance.Clone());
        tool.ParkingSpot = (source, target, face);
        if (Eddy.Selected is SelectionState.Trile selected && selected.Selected.Remove(source))
        {
            selected.Selected.Add(target);
        }
    }

    private void EraseTrile(TrileEmplacement emplacement)
    {
        if (!Level.Triles.TryGetValue(emplacement, out var instance))
        {
            return;
        }

        var before = instance.Clone();
        Level.Triles.Remove(emplacement);
        SyncTrile(emplacement, before, null);
        if (Eddy.Selected is SelectionState.Trile selected)
        {
            selected.Selected.Remove(emplacement);
        }
    }

    private void PaintOverlap(ToolState.Paint.Trile tool, TrileEmplacement emplacement, int slot)
    {
        if (!Level.Triles.TryGetValue(emplacement, out var main))
        {
            return;
        }

        var oldOverlap = GetOverlap(main, slot);
        var phi = ResolvePhi(oldOverlap ?? main);
        if (oldOverlap is { TrileId: var oldId, PhiLight: var oldPhi } && oldId == tool.Id && oldPhi == phi)
        {
            return;
        }

        if (!tool.Stroke.Add(new InstanceId.TrileOverlap(emplacement, slot)))
        {
            return;
        }

        var overlap = new TrileInstance
        {
            TrileId = tool.Id,
            PhiLight = phi,
            Position = main.Position
        };

        main.OverlappedTriles = main.OverlappedTriles.EmptyIfNull();
        while (main.OverlappedTriles.Count < slot)
        {
            main.OverlappedTriles.Add(new TrileInstance
            {
                TrileId = EddyEditor.InvalidId,
                Position = main.Position
            });
        }

        if (slot < main.OverlappedTriles.Count)
        {
            main.OverlappedTriles[slot] = overlap;
        }
        else
        {
            main.OverlappedTriles.Add(overlap);
        }

        SyncOverlap(emplacement, slot, oldOverlap, overlap.Clone());
    }

    private void EraseOverlap(TrileEmplacement emplacement, int slot)
    {
        if (!Level.Triles.TryGetValue(emplacement, out var main))
        {
            return;
        }

        var overlap = GetOverlap(main, slot);
        if (overlap == null)
        {
            return;
        }

        var before = overlap.Clone();
        main.OverlappedTriles!.RemoveAt(slot);
        main.OverlappedTriles = main.OverlappedTriles.NullIfEmpty();
        SyncOverlap(emplacement, slot, before, null);
    }

    private byte ResolvePhi(TrileInstance? source)
    {
        switch (Eddy.TrilePaintRotationMode)
        {
            case PaintRotationMode.Fixed fixedRotation:
                return fixedRotation.Phi;

            case PaintRotationMode.Random randomRotation:
                var phi = randomRotation.LastPhi;
                Eddy.TrilePaintRotationMode = randomRotation with { LastPhi = (byte)Random.Shared.Next(4) };
                return phi;

            case PaintRotationMode.Copy:
                return source?.PhiLight ?? 0;

            default:
                return 0;
        }
    }

    private byte GetHoveredPhi()
    {
        if (Eddy.HoveredTrile is { } hovered &&
            Level.Triles.TryGetValue(hovered.Trile.Emplacement, out var trile))
        {
            return trile.PhiLight;
        }

        return 0;
    }

    private static TrileInstance? GetOverlap(TrileInstance instance, int slot)
    {
        return instance.OverlappedTriles != null && slot >= 0 && slot < instance.OverlappedTriles.Count
            ? instance.OverlappedTriles[slot]
            : null;
    }

    private void SyncTrile(TrileEmplacement emplacement, TrileInstance? before, TrileInstance? after)
    {
        Eddy.Visualize(new InstanceId.TrileChange(emplacement, before, after));

        var beforeOverlaps = before?.OverlappedTriles.EmptyIfNull() ?? [];
        var afterOverlaps = after?.OverlappedTriles.EmptyIfNull() ?? [];
        for (var i = 0; i < Math.Max(beforeOverlaps.Count, afterOverlaps.Count); i++)
        {
            var beforeOverlap = i < beforeOverlaps.Count ? beforeOverlaps[i] : null;
            var afterOverlap = i < afterOverlaps.Count ? afterOverlaps[i] : null;
            if (!LevelDifference.SameTrile(beforeOverlap, afterOverlap))
            {
                Eddy.Visualize(new InstanceId.TrileOverlapChange(emplacement, i, beforeOverlap, afterOverlap));
            }
        }

        Eddy.Visualize(new InstanceId.CollisionMap());
        Eddy.Visualize(new InstanceId.PickableBounds());
    }

    private void SyncOverlap(TrileEmplacement emplacement, int slot, TrileInstance? before, TrileInstance? after)
    {
        Eddy.Visualize(new InstanceId.TrileOverlapChange(emplacement, slot, before, after));
        Eddy.Visualize(new InstanceId.CollisionMap());
        Eddy.Visualize(new InstanceId.PickableBounds());
    }
}
