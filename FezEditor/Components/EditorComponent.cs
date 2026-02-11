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

    protected IRenderingService RenderingService { get; }

    protected EditorComponent(Game game, string title)
    {
        Game = game;
        Title = title;
        History = new History();
        RenderingService = game.GetService<IRenderingService>();
    }

    public virtual void Update(GameTime gameTime)
    {
    }

    public virtual void Draw()
    {
    }

    public virtual void Dispose()
    {
    }
}