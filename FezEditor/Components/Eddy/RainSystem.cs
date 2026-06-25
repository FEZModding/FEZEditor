using FezEditor.Actors;
using FezEditor.Components.Eddy;
using FezEditor.Tools;

namespace FezEditor.Components.Eddy;

public class RainSystem : EddySystem
{
    private readonly Camera _camera;

    public RainSystem(Camera camera)
    {
        _camera = camera;
    }

    public override void Initialize()
    {
        Visualize(new InstanceId.Rain());
    }

    public override void Visualize(InstanceId instanceId)
    {
        if (instanceId is not InstanceId.Rain instance)
        {
            return;
        }

        var actor = Eddy.Registry.GetOrCreateActor(instance);
        actor.Name = "Rain";
        actor.Visible = Eddy.Visuals.HasFlag(EddyVisuals.Rain);

        if (Level.Rainy)
        {
            if (!actor.TryGetComponent<RainMesh>(out var mesh))
            {
                mesh = actor.AddComponent<RainMesh>();
            }

            mesh!.Camera = _camera;
            mesh.LevelSize = Level.Size.ToXna();
        }
        else
        {
            actor.RemoveComponent<RainMesh>();
        }
    }
}