using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Components.Eddy;

public abstract record InstanceId
{
    private InstanceId() { }

    public sealed record Trile(TrileEmplacement Emplacement) : InstanceId;

    public sealed record TrileOverlap(TrileEmplacement Emplacement, int Index) : InstanceId;

    public sealed record TrileBatch(int Id) : InstanceId;

    public sealed record TrileGroup(int Id) : InstanceId;

    public sealed record TrileChange(TrileEmplacement Emplacement, TrileInstance? Before, TrileInstance? After)
        : InstanceId;

    public sealed record TrileOverlapChange(
        TrileEmplacement Emplacement,
        int Index,
        TrileInstance? Before,
        TrileInstance? After) : InstanceId;

    public sealed record ArtObject(int Id) : InstanceId;

    public sealed record BackgroundPlane(int Id) : InstanceId;

    public sealed record NonPlayableCharacter(int Id) : InstanceId;

    public sealed record Gomez : InstanceId;

    public sealed record Volume(int Id) : InstanceId;

    public sealed record Path(int Id) : InstanceId;

    public sealed record GroupPath(int GroupId) : InstanceId;

    public sealed record PathWaypoint(InstanceId PathId, int Index) : InstanceId;

    public sealed record LevelBounds : InstanceId;

    public sealed record CollisionMap : InstanceId;

    public sealed record PickableBounds : InstanceId;

    public sealed record Sky : InstanceId;

    public sealed record Liquid : InstanceId;

    public sealed record Rain : InstanceId;

    public sealed record Level : InstanceId;

    public sealed override string ToString()
    {
        return this switch
        {
            Trile t => $"Trile({Format(t.Emplacement)})",
            TrileOverlap to => $"TrileOverlap({Format(to.Emplacement)}, {to.Index})",
            TrileBatch tb => $"TrileBatch({tb.Id})",
            TrileGroup tg => $"TrileGroup({tg.Id})",
            TrileChange tc => $"TrileChanged({Format(tc.Emplacement)})",
            TrileOverlapChange toc => $"TrileOverlapChanged({Format(toc.Emplacement)})",
            ArtObject ao => $"ArtObject({ao.Id})",
            BackgroundPlane bp => $"BackgroundPlane({bp.Id})",
            NonPlayableCharacter npc => $"NPC({npc.Id})",
            Gomez => "Gomez",
            Volume v => $"Volume({v.Id})",
            Path p => $"Path({p.Id})",
            GroupPath gp => $"GroupPath({gp.GroupId})",
            PathWaypoint pw => $"PathWaypoint({pw.PathId}, {pw.Index})",
            LevelBounds => "LevelBounds",
            CollisionMap => "CollisionMap",
            PickableBounds => "PickableBounds",
            Sky => "Sky",
            Liquid => "Liquid",
            Rain => "Rain",
            Level => "Level",
            _ => GetType().Name
        };
    }

    private static string Format(TrileEmplacement emplacement)
    {
        return $"{emplacement.X}, {emplacement.Y}, {emplacement.Z}";
    }
}

public static class InstanceIdExtensions
{
    public static int GetId(this InstanceId instance)
    {
        return instance switch
        {
            InstanceId.TrileBatch tb => tb.Id,
            InstanceId.TrileGroup tg => tg.Id,
            InstanceId.ArtObject ao => ao.Id,
            InstanceId.BackgroundPlane bp => bp.Id,
            InstanceId.NonPlayableCharacter npc => npc.Id,
            InstanceId.Volume v => v.Id,
            InstanceId.Path p => p.Id,
            InstanceId.GroupPath gp => gp.GroupId,
            _ => throw new ArgumentOutOfRangeException(nameof(instance), instance, null)
        };
    }

    public static bool TryGetSingle<T>(
        this IReadOnlyCollection<InstanceId> instanceIds,
        out T instance)
        where T : InstanceId
    {
        if (instanceIds.Count == 1 && instanceIds.First() is T typed)
        {
            instance = typed;
            return true;
        }

        instance = null!;
        return false;
    }
}