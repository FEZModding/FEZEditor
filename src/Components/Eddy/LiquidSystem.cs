using FezEditor.Actors;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Components.Eddy;

public class LiquidSystem : EddySystem
{
    public override void Initialize()
    {
        Visualize(new InstanceId.Liquid());
    }

    public override void Visualize(InstanceId instanceId)
    {
        if (instanceId is not InstanceId.Liquid instance)
        {
            return;
        }

        var actor = Eddy.Registry.GetOrCreateActor(instance);
        actor.Name = "Liquid";
        actor.Visible = Eddy.Visuals.HasFlag(EddyVisuals.Liquid);

        if (Level.WaterType != LiquidType.None)
        {
            if (!actor.TryGetComponent<LiquidMesh>(out var mesh))
            {
                mesh = actor.AddComponent<LiquidMesh>();
            }

            actor.Name = $"Water: {Level.WaterType}";
            mesh!.Visualize(Level.WaterType, Level.WaterHeight, Level.Size.ToXna());
        }
        else
        {
            actor.RemoveComponent<LiquidMesh>();
            actor.Name = "No Liquid";
        }
    }
}