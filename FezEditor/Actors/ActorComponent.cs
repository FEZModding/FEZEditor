using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public abstract class ActorComponent : IDisposable
{
    protected Actor Actor { get; private set; } = null!;

    protected Game Game { get; private set; } = null!;

    public bool Enabled { get; set; } = true;
        
    internal void Initialize(Game game, Actor host)
    {
        Game = game;
        Actor = host;
    }

    public virtual void Initialize()
    {
    }

    public virtual void Update(GameTime gameTime)
    {
    }

    public virtual void Dispose()
    {
    }
}