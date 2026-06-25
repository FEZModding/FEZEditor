using FezEditor.Actors;
using FezEditor.Components.Eddy;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Sky;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public class SkySystem : EddySystem
{
    private readonly Scene _scene;

    private readonly Camera _camera;

    private readonly Clock _clock;

    private string? _skyName;

    public SkySystem(Scene scene, Camera camera, Clock clock)
    {
        _scene = scene;
        _camera = camera;
        _clock = clock;
    }

    public override void Initialize()
    {
        Visualize(new InstanceId.Sky());
    }

    public override void Visualize(InstanceId instanceId)
    {
        if (instanceId is not InstanceId.Sky sky)
        {
            return;
        }

        _scene.Lighting.Ambient = Color.White * Level.BaseAmbient;
        _scene.Lighting.Diffuse = Color.White * Level.BaseDiffuse;

        var actor = Eddy.Registry.GetOrCreateActor(sky);
        actor.Name = $"Sky: {Level.SkyName}";
        actor.Visible = Eddy.Visuals.HasFlag(EddyVisuals.Sky);

        var created = false;
        if (!actor.TryGetComponent<SkyVisualizer>(out var visualizer))
        {
            visualizer = actor.AddComponent<SkyVisualizer>();
            visualizer.Initialize(_scene, _camera, _clock);
            created = true;
        }

        if (created || _skyName != Level.SkyName)
        {
            _skyName = Level.SkyName;
            var skyAsset = Resources.Load<Sky>("Skies/" + Level.SkyName);
            visualizer!.Visualize(skyAsset);
            visualizer.VisualizeShadows(skyAsset.Name, skyAsset.Shadows.EmptyIfNull());
        }

        visualizer!.LevelSize = Level.Size.ToXna();
    }
}