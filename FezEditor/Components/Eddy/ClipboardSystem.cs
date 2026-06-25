using System.Text.Json;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public class ClipboardSystem : EddySystem
{
    private Clipboard? _clipboard;

    public override void Update()
    {
        #region Paste

        if (_clipboard != null)
        {
            Status.AddHints(("Ctrl+V", "Paste"));
            if (ImGuiX.IsKeyShortcut(ImGuiKey.V))
            {
                using (Eddy.History.BeginScope("Paste Selection"))
                {
                    PasteClipboard(_clipboard);
                }
            }
        }

        #endregion

        if (Eddy.Selected is SelectionState.Empty)
        {
            return;
        }

        #region Delete

        Status.AddHints(("Delete", "Erase"));
        if (ImGui.IsKeyPressed(ImGuiKey.Delete))
        {
            using (Eddy.History.BeginScope("Delete Selection"))
            {
                RemoveSelected(Eddy.Selected);
            }

            Eddy.Selected = new SelectionState.Empty();
        }

        #endregion

        #region Copy

        Status.AddHints(("Ctrl+C", "Copy"));
        if (ImGuiX.IsKeyShortcut(ImGuiKey.C))
        {
            _clipboard = CreateClipboard(Eddy.Selected);
        }

        #endregion

        #region Cut

        Status.AddHints(("Ctrl+X", "Cut"));
        if (ImGuiX.IsKeyShortcut(ImGuiKey.X))
        {
            _clipboard = CreateClipboard(Eddy.Selected);
            using (Eddy.History.BeginScope("Cut Selected"))
            {
                RemoveSelected(Eddy.Selected);
            }

            Eddy.Selected = new SelectionState.Empty();
        }

        #endregion
    }

    private Clipboard? CreateClipboard(SelectionState selection)
    {
        var clipboard = new Clipboard();
        switch (selection)
        {
            case SelectionState.Instance instance:
                {
                    foreach (var id in instance.Selected)
                    {
                        switch (id)
                        {
                            case InstanceId.ArtObject ao when Level.ArtObjects.TryGetValue(ao.Id, out var value):
                                clipboard.ArtObjects[ao.Id] = Clone(value);
                                break;

                            case InstanceId.BackgroundPlane bp
                                when Level.BackgroundPlanes.TryGetValue(bp.Id, out var value):
                                clipboard.BackgroundPlanes[bp.Id] = Clone(value);
                                break;

                            case InstanceId.NonPlayableCharacter npc
                                when Level.NonPlayerCharacters.TryGetValue(npc.Id, out var value):
                                clipboard.Npcs[npc.Id] = Clone(value);
                                break;

                            case InstanceId.Volume v when Level.Volumes.TryGetValue(v.Id, out var inst):
                                clipboard.Volumes[v.Id] = Clone(inst);
                                break;

                            case InstanceId.Path p when Level.Paths.TryGetValue(p.Id, out var path):
                                clipboard.Paths[p] = Clone(path);
                                break;

                            case InstanceId.GroupPath gp when Level.Groups.TryGetValue(gp.GroupId, out var group) &&
                                                              group.Path != null:
                                clipboard.Paths[gp] = Clone(group.Path);
                                break;
                        }
                    }

                    clipboard.Origin = ComputeOrigin(clipboard);
                    break;
                }

            case SelectionState.Path p:
                {
                    switch (p.Selected)
                    {
                        case InstanceId.Path ip when Level.Paths.TryGetValue(ip.Id, out var path):
                            {
                                clipboard.Origin = ComputePathOrigin(path);
                                clipboard.Paths[ip] = Clone(path);
                                break;
                            }

                        case InstanceId.GroupPath gp when Level.Groups.TryGetValue(gp.GroupId, out var group) &&
                                                          group.Path != null:
                            {
                                clipboard.Origin = ComputePathOrigin(group.Path, ComputeGroupOffset(group));
                                clipboard.Paths[gp] = Clone(group.Path);
                                break;
                            }
                    }

                    break;
                }

            case SelectionState.Trile t:
                {
                    foreach (var emplacement in t.Selected)
                    {
                        if (Level.Triles.TryGetValue(emplacement, out var trile))
                        {
                            clipboard.Triles.Add(emplacement, Clone(trile));
                        }
                    }

                    clipboard.Origin = t.Anchor.ToXna().ToVector3();
                    break;
                }

            case SelectionState.TrileGroup tg:
                {
                    foreach (var groupId in tg.Selected)
                    {
                        if (!Level.Groups.TryGetValue(groupId, out var group))
                        {
                            continue;
                        }

                        clipboard.TrileGroups[groupId] = Clone(group);
                        foreach (var trile in group.Triles)
                        {
                            var emp = new TrileEmplacement(trile.Position);
                            clipboard.Triles[emp] = Clone(trile);
                        }
                    }

                    clipboard.Origin = ComputeOrigin(clipboard);
                    break;
                }
        }

        return clipboard.IsEmpty ? null : clipboard;
    }

    private void PasteClipboard(Clipboard clipboard)
    {
        #region Triles

        var changedTriles = new HashSet<InstanceId.Trile>();
        TrileEmplacement? anchor = null;

        if (clipboard.Triles.Count != 0 && clipboard.TrileGroups.Count <= 0)
        {
            var offset = ComputeTrilePasteOffset(clipboard);
            foreach (var (source, trile) in clipboard.Triles)
            {
                var target = source.Add(offset);
                var instance = Clone(trile);
                instance.Position = target.ToXna().ToVector3().ToRepacker();
                Level.Triles[target] = instance;
                changedTriles.Add(new InstanceId.Trile(target));

                if (clipboard.Triles.IndexOf(source) == 0)
                {
                    anchor = target;
                }
            }
        }

        #endregion

        #region Trile Groups

        var changedGroups = new HashSet<InstanceId.TrileGroup>();
        var changedInstances = new HashSet<InstanceId>();

        if (clipboard.TrileGroups.Count != 0)
        {
            var offset = ComputeTrilePasteOffset(clipboard);
            foreach (var sourceGroup in clipboard.TrileGroups.Values)
            {
                var groupId = NextId(Level.Groups.Keys);
                var group = Clone(sourceGroup);
                group.Triles.Clear();

                foreach (var trile in sourceGroup.Triles)
                {
                    var target = new TrileEmplacement(trile.Position).Add(offset);
                    var instance = Clone(trile);
                    instance.Position = target.ToXna().ToVector3().ToRepacker();
                    Level.Triles[target] = instance;
                    group.Triles.Add(instance);
                    changedTriles.Add(new InstanceId.Trile(target));
                }

                Level.Groups[groupId] = group;
                changedGroups.Add(new InstanceId.TrileGroup(groupId));

                if (group.Path != null)
                {
                    changedInstances.Add(new InstanceId.GroupPath(groupId));
                }
            }
        }

        #endregion

        #region Instances and Paths

        var pasteOffset = ComputeInstancePasteOffset(clipboard);

        foreach (var source in clipboard.ArtObjects.Values)
        {
            var id = NextId(Level.ArtObjects.Keys);
            var instance = Clone(source);
            instance.Position += pasteOffset;
            Level.ArtObjects[id] = instance;
            changedInstances.Add(new InstanceId.ArtObject(id));
        }

        foreach (var source in clipboard.BackgroundPlanes.Values)
        {
            var id = NextId(Level.BackgroundPlanes.Keys);
            var instance = Clone(source);
            instance.Position += pasteOffset;
            Level.BackgroundPlanes[id] = instance;
            changedInstances.Add(new InstanceId.BackgroundPlane(id));
        }

        foreach (var source in clipboard.Npcs.Values)
        {
            var id = NextId(Level.NonPlayerCharacters.Keys);
            var instance = Clone(source);
            instance.Position += pasteOffset;
            Level.NonPlayerCharacters[id] = instance;
            changedInstances.Add(new InstanceId.NonPlayableCharacter(id));
        }

        foreach (var source in clipboard.Volumes.Values)
        {
            var id = NextId(Level.Volumes.Keys);
            var instance = Clone(source);
            instance.From += pasteOffset;
            instance.To += pasteOffset;
            Level.Volumes[id] = instance;
            changedInstances.Add(new InstanceId.Volume(id));
        }

        foreach (var source in clipboard.Paths.Values)
        {
            var id = NextId(Level.Paths.Keys);
            var instance = Clone(source);
            foreach (var segment in instance.Segments)
            {
                segment.Destination += pasteOffset;
            }

            Level.Paths[id] = instance;
            changedInstances.Add(new InstanceId.Path(id));
        }

        #endregion

        #region Select changed instances

        if (changedGroups.Count > 0)
        {
            var groups = changedGroups.Select(tg => tg.Id).ToHashSet();
            Eddy.Selected = new SelectionState.TrileGroup(groups);
        }
        else if (changedTriles.Count > 0 && anchor != null)
        {
            var triles = changedTriles.Select(t => t.Emplacement).ToList();
            Eddy.Selected = new SelectionState.Trile(triles, FaceOrientation.Top, anchor);
        }
        else if (changedInstances.Count > 0)
        {
            Eddy.Selected = new SelectionState.Instance(changedInstances);
        }

        #endregion
    }

    private void RemoveSelected(SelectionState selection)
    {
        switch (selection)
        {
            case SelectionState.Trile t:
                {
                    if (Eddy.OverlapIndex > 0)
                    {
                        var slot = Eddy.OverlapIndex - 1;
                        foreach (var emplacement in t.Selected)
                        {
                            if (Level.Triles.TryGetValue(emplacement, out var trile) &&
                                trile.OverlappedTriles != null &&
                                slot < trile.OverlappedTriles.Count)
                            {
                                trile.OverlappedTriles.RemoveAt(slot);
                                trile.OverlappedTriles = trile.OverlappedTriles.NullIfEmpty();
                            }
                        }

                        break;
                    }

                    foreach (var emplacement in t.Selected)
                    {
                        Level.Triles.Remove(emplacement);
                    }

                    foreach (var group in Level.Groups.Values)
                    {
                        group.Triles.RemoveAll(trile => t.Selected.Contains(new TrileEmplacement(trile.Position)));
                    }

                    break;
                }

            case SelectionState.TrileGroup tg:
                {
                    foreach (var groupId in tg.Selected)
                    {
                        if (Level.Groups.Remove(groupId, out var group))
                        {
                            foreach (var trile in group.Triles)
                            {
                                var emplacement = new TrileEmplacement(trile.Position);
                                Level.Triles.Remove(emplacement);
                            }
                        }
                    }

                    break;
                }

            case SelectionState.Path p:
                {
                    var path = p.Selected switch
                    {
                        InstanceId.Path lp when Level.Paths.TryGetValue(lp.Id, out var levelPath) => levelPath,
                        InstanceId.GroupPath gp when Level.Groups.TryGetValue(gp.GroupId, out var group) => group.Path,
                        _ => null
                    };

                    if (path == null)
                    {
                        break;
                    }

                    if (p.Waypoints.Count == 0)
                    {
                        RemovePath(p.Selected);
                        break;
                    }

                    foreach (var index in p.Waypoints.OrderDescending())
                    {
                        if (index >= 0 && index < path.Segments.Count)
                        {
                            path.Segments.RemoveAt(index);
                        }
                    }

                    if (path.Segments.Count == 0)
                    {
                        RemovePath(p.Selected);
                    }

                    break;
                }

            case SelectionState.Instance i:
                {
                    foreach (var instance in i.Selected)
                    {
                        switch (instance)
                        {
                            case InstanceId.ArtObject ao:
                                Level.ArtObjects.Remove(ao.Id);
                                break;

                            case InstanceId.BackgroundPlane bp:
                                Level.BackgroundPlanes.Remove(bp.Id);
                                break;

                            case InstanceId.NonPlayableCharacter npc:
                                Level.NonPlayerCharacters.Remove(npc.Id);
                                break;

                            case InstanceId.Volume volume:
                                Level.Volumes.Remove(volume.Id);
                                break;

                            case InstanceId.Path or InstanceId.GroupPath:
                                RemovePath(instance);
                                break;
                        }
                    }

                    break;
                }
        }
    }

    private void RemovePath(InstanceId instance)
    {
        switch (instance)
        {
            case InstanceId.Path path:
                Level.Paths.Remove(path.Id);
                break;

            case InstanceId.GroupPath groupPath when Level.Groups.TryGetValue(groupPath.GroupId, out var group):
                group.Path = null;
                break;
        }
    }

    private static Vector3 ComputeOrigin(Clipboard clipboard)
    {
        var points = new List<Vector3>();

        points.AddRange(clipboard.Triles.Keys
            .Select(e => e.ToXna().ToVector3() + new Vector3(0.5f)));

        points.AddRange(clipboard.ArtObjects.Values
            .Select(i => i.Position.ToXna()));

        points.AddRange(clipboard.BackgroundPlanes.Values
            .Select(i => i.Position.ToXna()));

        points.AddRange(clipboard.Npcs.Values
            .Select(i => i.Position.ToXna()));

        points.AddRange(clipboard.Volumes.Values
            .Select(v => (v.From.ToXna() + v.To.ToXna()) / 2f));

        points.AddRange(clipboard.Paths.Values.Select(path => ComputePathOrigin(path)));

        return points.Count == 0
            ? Vector3.Zero
            : points.Aggregate(Vector3.Zero, (sum, p) => sum + p) / points.Count;
    }

    private static Vector3 ComputePathOrigin(MovementPath path, Vector3 offset = default)
    {
        if (path.Segments.Count == 0)
        {
            return offset;
        }

        return path.Segments
                   .Aggregate(Vector3.Zero, (current, segment) => current + (offset + segment.Destination.ToXna())) /
               path.Segments.Count;
    }

    private static Vector3 ComputeGroupOffset(TrileGroup group)
    {
        return group.Triles.Count == 0
            ? Vector3.Zero
            : group.Triles
                .Select(trile => trile.Position.ToXna())
                .Aggregate(Vector3.Zero, (sum, position) => sum + position) / group.Triles.Count;
    }

    private static int NextId(IEnumerable<int> ids)
    {
        return ids.Where(id => id != EddyEditor.InvalidId).DefaultIfEmpty(-1).Max() + 1;
    }

    private TrileEmplacement ComputeTrilePasteOffset(Clipboard clipboard)
    {
        if (Eddy.HoveredTrile is not { } hovered)
        {
            return new TrileEmplacement();
        }

        var step = new TrileEmplacement(hovered.Face.AsVector().ToRepacker());
        var target = hovered.Trile.Emplacement.Add(step);

        var origin = clipboard.Origin ?? Vector3.Zero;
        var source = new TrileEmplacement(origin.ToRepacker());

        return target.Sub(source);
    }

    private RVector3 ComputeInstancePasteOffset(Clipboard clipboard)
    {
        if (Eddy.HoveredTrile is not { } hovered)
        {
            return RVector3.Zero;
        }

        var origin = (clipboard.Origin ?? Vector3.Zero).ToRepacker();
        var target = hovered.Trile.Emplacement.AsVector() + new RVector3(0.5f, 0.5f, 0.5f);
        return target - origin;
    }

    private static T Clone<T>(T value) where T : class
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    }

    private sealed class Clipboard
    {
        public Vector3? Origin { get; set; }

        public OrderedDictionary<TrileEmplacement, TrileInstance> Triles { get; } = new();

        public Dictionary<int, TrileGroup> TrileGroups { get; } = new();

        public Dictionary<int, ArtObjectInstance> ArtObjects { get; } = new();

        public Dictionary<int, BackgroundPlane> BackgroundPlanes { get; } = new();

        public Dictionary<int, NpcInstance> Npcs { get; } = new();

        public Dictionary<int, Volume> Volumes { get; } = new();

        public Dictionary<InstanceId, MovementPath> Paths { get; } = new();

        public bool IsEmpty =>
            Triles.Count == 0 &&
            TrileGroups.Count == 0 &&
            ArtObjects.Count == 0 &&
            BackgroundPlanes.Count == 0 &&
            Npcs.Count == 0 &&
            Volumes.Count == 0 &&
            Paths.Count == 0;
    }
}