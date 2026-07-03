using FezEditor.Actors;
using FezEditor.Components.Eddy;
using FezEditor.Tools;

namespace FezEditor.Components.Eddy;

public class LevelBoundsSystem : EddySystem
{
    public override void Initialize()
    {
        Visualize(new InstanceId.LevelBounds());
    }

    public override void Visualize(InstanceId instanceId)
    {
        if (instanceId is not InstanceId.LevelBounds lb)
        {
            return;
        }

        var actor = Eddy.Registry.GetOrCreateActor(lb);
        actor.Name = "Level Bounds";
        actor.Visible = Eddy.Visuals.HasFlag(EddyVisuals.LevelBounds);

        if (!actor.TryGetComponent<BoundsMesh>(out var mesh))
        {
            mesh = actor.AddComponent<BoundsMesh>();
        }

        mesh!.Size = Level.Size.ToXna();
    }
}