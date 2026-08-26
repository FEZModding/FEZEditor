using FezEditor.Actors;

namespace FezEditor.Components.Eddy;

public class PickableBoundsSystem : EddySystem
{
    public override void Initialize()
    {
        Visualize(new InstanceId.PickableBounds());
    }

    public override void Visualize(InstanceId instanceId)
    {
        if (instanceId is not InstanceId.PickableBounds pb)
        {
            return;
        }

        var actor = Eddy.Registry.GetOrCreateActor(pb);
        actor.Name = "Pickable Bounds";

        actor.Visible = Eddy.Visuals.HasFlag(EddyVisuals.PickableBounds);
        if (!actor.Visible)
        {
            return;
        }

        if (!actor.TryGetComponent<PickableBounds>(out var mesh))
        {
            mesh = actor.AddComponent<PickableBounds>();
        }

        mesh!.Visualize(Eddy.Registry.Actors);
    }
}