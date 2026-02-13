using FezEditor.Services;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class Transform : ActorComponent
{
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    
    public Vector3 Scale { get; set; }

    private RenderingService _rendering = null!;

    public override void Initialize()
    {
        _rendering = Game.GetService<RenderingService>();
        Position = _rendering.InstanceGetPosition(Actor.InstanceRid);
        Rotation = _rendering.InstanceGetRotation(Actor.InstanceRid);
        Scale = _rendering.InstanceGetScale(Actor.InstanceRid);
    }

    public override void Update(GameTime gameTime)
    {
        _rendering.InstanceSetPosition(Actor.InstanceRid, Position);
        _rendering.InstanceSetRotation(Actor.InstanceRid, Rotation);
        _rendering.InstanceSetScale(Actor.InstanceRid, Scale);
    }
}