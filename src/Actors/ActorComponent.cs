using FezEditor.Tools;
using FezEditor.Structure;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public abstract class ActorComponent : IComponent
{
    protected Actor Actor { get; }

    protected Game Game { get; }

    public bool Enabled { get; set; } = true;

    public InputHints? Hints { get; set; }

    internal ActorComponent(Game game, Actor actor)
    {
        Game = game;
        Actor = actor;
    }

    public virtual void LoadContent(IContentManager content)
    {
    }

    public virtual void Update(GameTime gameTime)
    {
    }

    public virtual void Dispose()
    {
    }
}