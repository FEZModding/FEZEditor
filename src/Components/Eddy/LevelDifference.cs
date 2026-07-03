using System.Text.Json;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Components.Eddy;

public static class LevelDifference
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        Converters = { new TrileEmplacementConverter() }
    };

    public static IEnumerable<InstanceId> Get(History.Change change)
    {
        var emitted = new HashSet<InstanceId>();
        foreach (var id in Enumerate(change))
        {
            if (emitted.Add(id))
            {
                yield return id;
            }
        }
    }

    private static IEnumerable<InstanceId> Enumerate(History.Change change)
    {
        if (string.IsNullOrEmpty(change.BeforeJson) || string.IsNullOrEmpty(change.AfterJson))
        {
            yield break;
        }

        var before = JsonSerializer.Deserialize<Level>(change.BeforeJson, JsonOptions)!;
        var after = JsonSerializer.Deserialize<Level>(change.AfterJson, JsonOptions)!;

        foreach (var id in DiffDict(before.ArtObjects, after.ArtObjects,
                     id => new InstanceId.ArtObject(id)))
        {
            yield return id;
        }

        foreach (var id in DiffDict(before.BackgroundPlanes, after.BackgroundPlanes,
                     id => new InstanceId.BackgroundPlane(id)))
        {
            yield return id;
        }

        foreach (var id in DiffDict(before.NonPlayerCharacters, after.NonPlayerCharacters,
                     id => new InstanceId.NonPlayableCharacter(id)))
        {
            yield return id;
        }

        foreach (var id in DiffDict(before.Volumes, after.Volumes,
                     id => new InstanceId.Volume(id)))
        {
            yield return id;
        }

        foreach (var id in DiffDict(before.Paths, after.Paths,
                     id => new InstanceId.Path(id)))
        {
            yield return id;
        }

        foreach (var id in DiffTriles(before, after))
        {
            yield return id;
        }

        foreach (var id in DiffGroups(before, after))
        {
            yield return id;
        }

        foreach (var id in DiffLevelProperties(before, after))
        {
            yield return id;
        }
    }

    private static IEnumerable<InstanceId> DiffDict<TKey, TValue>(
        IDictionary<TKey, TValue> before,
        IDictionary<TKey, TValue> after,
        Func<TKey, InstanceId> toId)
        where TKey : notnull
    {
        foreach (var key in before.Keys.Union(after.Keys))
        {
            if (!SameValue(before, after, key))
            {
                yield return toId(key);
                yield return new InstanceId.PickableBounds();
            }
        }
    }

    private static IEnumerable<InstanceId> DiffTriles(Level before, Level after)
    {
        foreach (var emp in before.Triles.Keys.Union(after.Triles.Keys))
        {
            if (SameTrileValue(before.Triles, after.Triles, emp))
            {
                continue;
            }

            var beforeMain = before.Triles.TryGetValue(emp, out var oldTrile) ? oldTrile : null;
            var afterMain = after.Triles.TryGetValue(emp, out var newTrile) ? newTrile : null;

            yield return new InstanceId.TrileChange(emp, beforeMain, afterMain);
            yield return new InstanceId.CollisionMap();
            yield return new InstanceId.PickableBounds();

            var beforeOverlaps = beforeMain?.OverlappedTriles.EmptyIfNull() ?? [];
            var afterOverlaps = afterMain?.OverlappedTriles.EmptyIfNull() ?? [];

            for (var i = 0; i < Math.Max(beforeOverlaps.Count, afterOverlaps.Count); i++)
            {
                var beforeOverlap = i < beforeOverlaps.Count ? beforeOverlaps[i] : null;
                var afterOverlap = i < afterOverlaps.Count ? afterOverlaps[i] : null;

                if (SameTrile(beforeOverlap, afterOverlap))
                {
                    continue;
                }

                yield return new InstanceId.TrileOverlapChange(emp, i, beforeOverlap, afterOverlap);
            }
        }
    }

    private static IEnumerable<InstanceId> DiffGroups(Level before, Level after)
    {
        foreach (var id in before.Groups.Keys.Union(after.Groups.Keys))
        {
            if (!SameGroupValue(before.Groups, after.Groups, id))
            {
                yield return new InstanceId.TrileGroup(id);
                yield return new InstanceId.GroupPath(id);
            }
        }
    }

    private static IEnumerable<InstanceId> DiffLevelProperties(Level before, Level after)
    {
        if (!before.Size.Equals(after.Size))
        {
            yield return new InstanceId.LevelBounds();
            yield return new InstanceId.Sky();
            yield return new InstanceId.Liquid();
            yield return new InstanceId.Rain();
        }

        if (!SameTrileFace(before.StartingFace, after.StartingFace))
        {
            yield return new InstanceId.Gomez();
        }

        if (!before.BaseDiffuse.Equals(after.BaseDiffuse) ||
            !before.BaseAmbient.Equals(after.BaseAmbient))
        {
            yield return new InstanceId.Sky();
        }

        if (!SameNullableString(before.GomezHaloName, after.GomezHaloName) ||
            before.HaloFiltering != after.HaloFiltering)
        {
            yield return new InstanceId.Gomez();
        }

        if (before.WaterType != after.WaterType ||
            !before.WaterHeight.Equals(after.WaterHeight))
        {
            yield return new InstanceId.Liquid();
        }

        if (!before.SkyName.Equals(after.SkyName))
        {
            yield return new InstanceId.Sky();
        }

        if (before.Rainy != after.Rainy)
        {
            yield return new InstanceId.Rain();
        }
    }

    private static bool SameValue<TKey, TValue>(
        IDictionary<TKey, TValue> before,
        IDictionary<TKey, TValue> after,
        TKey key)
        where TKey : notnull
    {
        var beforeHasValue = before.TryGetValue(key, out var oldValue);
        var afterHasValue = after.TryGetValue(key, out var newValue);

        if (beforeHasValue != afterHasValue)
        {
            return false;
        }

        if (!beforeHasValue)
        {
            return true;
        }

        return JsonSerializer.Serialize(oldValue, JsonOptions)
            .Equals(JsonSerializer.Serialize(newValue, JsonOptions));
    }

    private static bool SameTrileValue(
        IDictionary<TrileEmplacement, TrileInstance> before,
        IDictionary<TrileEmplacement, TrileInstance> after,
        TrileEmplacement key)
    {
        return before.TryGetValue(key, out var oldValue) &&
               after.TryGetValue(key, out var newValue) &&
               SameTrile(oldValue, newValue);
    }

    private static bool SameGroupValue(
        IDictionary<int, TrileGroup> before,
        IDictionary<int, TrileGroup> after,
        int key)
    {
        return before.TryGetValue(key, out var oldValue) &&
               after.TryGetValue(key, out var newValue) &&
               SameGroup(oldValue, newValue);
    }

    public static bool SameTrile(TrileInstance? before, TrileInstance? after)
    {
        if (ReferenceEquals(before, after))
        {
            return true;
        }

        if (before is null || after is null)
        {
            return false;
        }

        return before.TrileId == after.TrileId &&
               before.PhiLight == after.PhiLight &&
               before.Position.Equals(after.Position) &&
               SameActorSettings(before.ActorSettings, after.ActorSettings) &&
               SameOverlaps(before.OverlappedTriles, after.OverlappedTriles);
    }

    private static bool SameActorSettings(TrileInstanceActorSettings? before, TrileInstanceActorSettings? after)
    {
        if (ReferenceEquals(before, after))
        {
            return true;
        }

        if (before is null || after is null)
        {
            return false;
        }

        return before.ContainedTrile == after.ContainedTrile &&
               before.SignText == after.SignText &&
               before.Sequence.EmptyIfNull().SequenceEqual(after.Sequence.EmptyIfNull()) &&
               before.SequenceSampleName == after.SequenceSampleName &&
               before.SequenceAlternateSampleName == after.SequenceAlternateSampleName &&
               before.HostVolume == after.HostVolume;
    }

    private static bool SameOverlaps(List<TrileInstance>? before, List<TrileInstance>? after)
    {
        var oldOverlaps = before.EmptyIfNull();
        var newOverlaps = after.EmptyIfNull();
        if (oldOverlaps.Count != newOverlaps.Count)
        {
            return false;
        }

        return !oldOverlaps.Where((t, i) => !SameTrile(t, newOverlaps[i])).Any();
    }

    private static bool SameGroup(TrileGroup before, TrileGroup after)
    {
        return before.Heavy == after.Heavy &&
               before.ActorType == after.ActorType &&
               before.GeyserOffset.Equals(after.GeyserOffset) &&
               before.GeyserPauseFor.Equals(after.GeyserPauseFor) &&
               before.GeyserLiftFor.Equals(after.GeyserLiftFor) &&
               before.GeyserApexHeight.Equals(after.GeyserApexHeight) &&
               before.SpinCenter.Equals(after.SpinCenter) &&
               before.SpinClockwise == after.SpinClockwise &&
               before.SpinFrequency.Equals(after.SpinFrequency) &&
               before.SpinNeedsTriggering == after.SpinNeedsTriggering &&
               before.Spin180Degrees == after.Spin180Degrees &&
               before.FallOnRotate == after.FallOnRotate &&
               before.SpinOffset.Equals(after.SpinOffset) &&
               before.AssociatedSound == after.AssociatedSound &&
               SamePath(before.Path, after.Path) &&
               before.Triles
                   .Select(trile => new TrileEmplacement(trile.Position))
                   .SequenceEqual(after.Triles.Select(trile => new TrileEmplacement(trile.Position)));
    }

    private static bool SamePath(MovementPath? before, MovementPath? after)
    {
        if (ReferenceEquals(before, after))
        {
            return true;
        }

        if (before is null || after is null)
        {
            return false;
        }

        return before.NeedsTrigger == after.NeedsTrigger &&
               before.EndBehavior == after.EndBehavior &&
               SameNullableString(before.SoundName, after.SoundName) &&
               before.IsSpline == after.IsSpline &&
               before.OffsetSeconds.Equals(after.OffsetSeconds) &&
               before.SaveTrigger == after.SaveTrigger &&
               SamePathSegments(before.Segments, after.Segments);
    }

    private static bool SamePathSegments(List<PathSegment> oldSegments, List<PathSegment> newSegments)
    {
        if (oldSegments.Count != newSegments.Count)
        {
            return false;
        }

        return !oldSegments.Where((ps, i) => !SamePathSegment(ps, newSegments[i])).Any();
    }

    private static bool SamePathSegment(PathSegment before, PathSegment after)
    {
        return before.Destination.Equals(after.Destination) &&
               before.Duration.Equals(after.Duration) &&
               before.WaitTimeOnStart.Equals(after.WaitTimeOnStart) &&
               before.WaitTimeOnFinish.Equals(after.WaitTimeOnFinish) &&
               before.Acceleration.Equals(after.Deceleration) &&
               before.Deceleration.Equals(after.Deceleration) &&
               before.JitterFactor.Equals(after.JitterFactor) &&
               before.Orientation.Equals(after.Orientation) &&
               SameCameraNodeData(before.CustomData, after.CustomData);
    }

    private static bool SameCameraNodeData(CameraNodeData? before, CameraNodeData? after)
    {
        if (ReferenceEquals(before, after))
        {
            return true;
        }

        if (before is null || after is null)
        {
            return false;
        }

        return before.Perspective == after.Perspective &&
               before.PixelsPerTrixel == after.PixelsPerTrixel &&
               SameNullableString(before.SoundName, after.SoundName);
    }

    private static bool SameNullableString(string? before, string? after)
    {
        if (before is null || after is null)
        {
            return false;
        }

        return before.Equals(after);
    }

    private static bool SameTrileFace(TrileFace? before, TrileFace? after)
    {
        if (ReferenceEquals(before, after))
        {
            return true;
        }

        if (before is null || after is null)
        {
            return false;
        }

        return before.Face == after.Face &&
               before.Id.Equals(after.Id);
    }
}