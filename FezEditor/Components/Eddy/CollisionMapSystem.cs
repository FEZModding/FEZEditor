using FezEditor.Actors;
using FezEditor.Components.Eddy;
using FezEditor.Tools;

namespace FezEditor.Components.Eddy;

public class CollisionMapSystem : EddySystem
{
    public override void Initialize()
    {
        Visualize(new InstanceId.CollisionMap());
    }

    public override void Visualize(InstanceId instanceId)
    {
        if (instanceId is not InstanceId.CollisionMap cm)
        {
            return;
        }

        var actor = Eddy.Registry.GetOrCreateActor(cm);
        actor.Name = $"Collision Map: {Level.TrileSetName}";

        actor.Visible = Eddy.Visuals.HasFlag(EddyVisuals.CollisionMap);
        if (!actor.Visible)
        {
            return;
        }

        if (!actor.TryGetComponent<TrileCollisionMesh>(out var mesh))
        {
            mesh = actor.AddComponent<TrileCollisionMesh>();
        }

        mesh!.ClearInstanceData();

        foreach (var instance in Level.Triles.Values.Where(ti => ti.TrileId != EddyEditor.InvalidId))
        {
            var trile = Eddy.TrileSet.Triles[instance.TrileId];
            mesh.AddInstanceData(instance.Position.ToXna(), trile.Faces, trile.Size.ToXna(),
                trile.Offset.ToXna(), instance.PhiLight);

            foreach (var overlapped in instance.OverlappedTriles.EmptyIfNull())
            {
                if (overlapped.TrileId != EddyEditor.InvalidId)
                {
                    var overlappedTrile = Eddy.TrileSet.Triles[overlapped.TrileId];
                    mesh.AddInstanceData(
                        overlapped.Position.ToXna(), overlappedTrile.Faces, overlappedTrile.Size.ToXna(),
                        overlappedTrile.Offset.ToXna(), overlapped.PhiLight);
                }
            }
        }
    }
}