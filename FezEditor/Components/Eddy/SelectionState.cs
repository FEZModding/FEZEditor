using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Components.Eddy;

public abstract record SelectionState
{
    private SelectionState() { }

    public sealed record Empty : SelectionState;

    public sealed record Instance(HashSet<InstanceId> Selected) : SelectionState;

    public sealed record Trile(List<TrileEmplacement> Selected, FaceOrientation Face, TrileEmplacement Anchor) : SelectionState;

    public sealed record TrileGroup(HashSet<int> Selected) : SelectionState;

    public sealed record Path(InstanceId Selected, HashSet<int> Waypoints) : SelectionState;

    public sealed override string ToString()
    {
        return this switch
        {
            Empty => "Empty",

            Instance i => i.Selected.Count == 0
                ? "Instances(none)"
                : $"Instances({string.Join(", ", i.Selected)})",

            Trile t => $"Triles(count={t.Selected.Count}, face={t.Face}, anchor={Format(t.Anchor)})",

            TrileGroup tg => tg.Selected.Count == 0
                ? "TrileGroups(none)"
                : $"TrileGroups({FormatIntegers(tg.Selected)})",

            Path p => $"Path({p.Selected}, waypoints={FormatIntegers(p.Waypoints)})",

            _ => GetType().Name
        };
    }

    private static string FormatIntegers(HashSet<int> selected)
    {
        return selected.Count == 0 ? "none" : string.Join(", ", selected.Order());
    }

    private static string Format(TrileEmplacement emplacement)
    {
        return $"{emplacement.X}, {emplacement.Y}, {emplacement.Z}";
    }
}