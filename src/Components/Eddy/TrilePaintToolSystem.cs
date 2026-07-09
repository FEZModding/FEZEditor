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

        if (tool.RotationMode is PaintRotationMode.Fixed @fixed &&
            Input.CaptureScrollWheelDelta(out var scroll))
        {
            var delta = scroll > 0 ? 1 : -1;
            var phi = (byte)((@fixed.Phi + delta + 4) % 4);
            tool.RotationMode = new PaintRotationMode.Fixed(phi);
        }

        if (ImGui.IsKeyPressed(ImGuiKey.R))
        {
            tool.RotationMode = tool.RotationMode switch
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
            Status.AddHint("LMB", $"Paint Layer {Eddy.OverlapIndex}");
            Status.AddHint("Ctrl+LMB", $"Erase Layer {Eddy.OverlapIndex}");
        }
        else
        {
            Status.AddHint("LMB", "Paint");
            Status.AddHint("Shift+LMB", "Append");
            Status.AddHint("Ctrl+LMB", "Erase");
        }

        Status.AddHint("R", $"Rotate: {tool.RotationMode.DisplayName}");

        #endregion

        #region Handle inputs

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Eddy.Tool = new ToolState.Select();
            return;
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            Eddy.Tool.Clear();
            return;
        }

        if (Eddy.HoveredTrile is not { } hovered ||
            (!ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsMouseDragging(ImGuiMouseButton.Left)))
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
            !selection.Selected.Contains(hovered.Trile.Emplacement) ||
            ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            PaintTriles(tool, hovered.Face, [hovered.Trile.Emplacement]);
        }
        else if (selection.Selected.Count > 0)
        {
            PaintTriles(tool, selection.Face, selection.Selected.ToArray());
        }

        #endregion
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
                var slot = Eddy.OverlapIndex - 1;
                if (ImGui.GetIO().KeyCtrl)
                {
                    EraseOverlap(tool, emplacement, slot);
                }
                else
                {
                    PaintOverlap(tool, emplacement, slot);
                }

                continue;
            }

            if (ImGui.GetIO().KeyShift)
            {
                AppendTrile(tool, emplacement, face);
            }
            else if (ImGui.GetIO().KeyCtrl)
            {
                EraseTrile(tool, emplacement);
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

        var phi = ResolvePhi(tool, instance);
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

        var phi = ResolvePhi(tool, sourceTrile);
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

    private void EraseTrile(ToolState.Paint.Trile tool, TrileEmplacement emplacement)
    {
        if (!Level.Triles.TryGetValue(emplacement, out var instance))
        {
            return;
        }

        if (!tool.Stroke.Add(new InstanceId.Trile(emplacement)))
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
        var phi = ResolvePhi(tool, oldOverlap ?? main);
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

    private void EraseOverlap(ToolState.Paint.Trile tool, TrileEmplacement emplacement, int slot)
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

        if (!tool.Stroke.Add(new InstanceId.TrileOverlap(emplacement, slot)))
        {
            return;
        }

        var before = overlap.Clone();
        main.OverlappedTriles!.RemoveAt(slot);
        main.OverlappedTriles = main.OverlappedTriles.NullIfEmpty();
        SyncOverlap(emplacement, slot, before, null);
    }

    private static byte ResolvePhi(ToolState.Paint.Trile tool, TrileInstance? source)
    {
        switch (tool.RotationMode)
        {
            case PaintRotationMode.Fixed fixedRotation:
                return fixedRotation.Phi;

            case PaintRotationMode.Random randomRotation:
                var phi = randomRotation.LastPhi;
                tool.RotationMode = randomRotation with { LastPhi = (byte)Random.Shared.Next(4) };
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