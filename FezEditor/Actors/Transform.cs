using FezEditor.Services;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class Transform : ActorComponent
{
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    
    public Vector3 Scale { get; set; }

    private readonly RenderingService _rendering;

    internal Transform(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        Position = _rendering.InstanceGetPosition(actor.InstanceRid);
        Rotation = _rendering.InstanceGetRotation(actor.InstanceRid);
        Scale = _rendering.InstanceGetScale(actor.InstanceRid);
    }

    public override void Update(GameTime gameTime)
    {
        _rendering.InstanceSetPosition(Actor.InstanceRid, Position);
        _rendering.InstanceSetRotation(Actor.InstanceRid, Rotation);
        _rendering.InstanceSetScale(Actor.InstanceRid, Scale);
    }
}