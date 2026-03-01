using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public abstract class EditorComponent
{
    public string Title { get; }

    public virtual object Asset => null!;

    public History History { get; }

    protected Game Game { get; }

    protected InputService InputService { get; }
    
    protected ResourceService ResourceService { get; }
    
    protected IContentManager ContentManager { get; }

    protected EditorComponent(Game game, string title)
    {
        Game = game;
        Title = title;
        History = new History();
        InputService = game.GetService<InputService>();
        ResourceService = game.GetService<ResourceService>();
        ContentManager = game.GetService<ContentService>().Get(this);
    }

    public virtual void LoadContent()
    {
    }

    public virtual void Update(GameTime gameTime)
    {
    }

    public virtual void Draw()
    {
    }

    public virtual void Dispose()
    {
        Game.GetService<ContentService>().Unload(this);
    }
}