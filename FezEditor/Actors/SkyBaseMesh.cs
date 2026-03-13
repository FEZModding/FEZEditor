using FezEditor.Services;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public abstract class SkyBaseMesh : ActorComponent
{
    protected static readonly Vector3 BaseScale = new(1, 5, 1);

    protected const float ExtraScale = 40f;

    protected const float TimeScaleFactor = 13f / 18f;

    public SkyVisualizer Sky
    {
        protected get
        {
            if (_sky == null)
            {
                throw new InvalidOperationException("Sky is missing");
            }

            return _sky;
        }
        set => _sky = value;
    }

    protected readonly RenderingService _rendering;

    protected readonly ResourceService _resources;

    private SkyVisualizer? _sky;

    protected SkyBaseMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _resources = game.GetService<ResourceService>();
    }
}