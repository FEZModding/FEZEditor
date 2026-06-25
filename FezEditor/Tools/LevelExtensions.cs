using FEZRepacker.Core.Definitions.Game.Level;
using Microsoft.Xna.Framework;

namespace FezEditor.Tools;

public static class LevelExtensions
{
    public static IDictionary<TrileEmplacement, int> GetEmplacementGroups(this Level level)
    {
        var groupEmplacements = new Dictionary<TrileEmplacement, int>();

        foreach (var (id, group) in level.Groups)
        {
            foreach (var instance in group.Triles)
            {
                // Grouped emplacements are stored in TrileInstance objects.
                // Check FEZRepacker.Core.Definitions.Json.TrileGroupJsonModel
                var emp = new TrileEmplacement(instance.Position);
                groupEmplacements[emp] = id;
            }
        }

        return groupEmplacements;
    }

    public static HashSet<TrileEmplacement> GetGroupSiblingEmplacements(this Level level, TrileEmplacement emplacement)
    {
        var groupEmplacements = new HashSet<TrileEmplacement>();

        foreach (var group in level.Groups.Values)
        {
            var position = new RVector3(emplacement.X, emplacement.Y, emplacement.Z);
            if (group.Triles.Any(t => t.Position.Equals(position)))
            {
                foreach (var te in group.Triles.Select(ti => new TrileEmplacement(ti.Position)))
                {
                    groupEmplacements.Add(te);
                }

                break;
            }
        }

        return groupEmplacements;
    }

    public static TrileInstance Clone(this TrileInstance instance)
    {
        return new TrileInstance
        {
            TrileId = instance.TrileId,
            PhiLight = instance.PhiLight,
            Position = instance.Position,
            ActorSettings = instance.ActorSettings,
            OverlappedTriles = instance.OverlappedTriles?.Select(Clone).ToList()
        };
    }
}