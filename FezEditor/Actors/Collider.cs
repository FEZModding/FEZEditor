using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class Collider : ActorComponent
{
    public BoundingBox BoundingBox { get; private set; }
    
    public Vector3 Size { get; set; }

    private readonly Transform _transform;
    
    internal Collider(Game game, Actor actor) : base(game, actor)
    {
        _transform = actor.GetComponent<Transform>();
    }

    public override void Update(GameTime gameTime)
    {
        BoundingBox = Mathz.ComputeBoundingBox(
            _transform.Position, _transform.Rotation,
            _transform.Scale, Size
        );
    }
}