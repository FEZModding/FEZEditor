using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public abstract class ActorComponent : IDisposable
{
    protected Actor Actor { get; }

    protected Game Game { get; }

    public bool Enabled { get; set; } = true;

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