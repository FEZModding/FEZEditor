using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public abstract class EditorComponent
{
    public string Title { get; set; }

    public virtual object Asset => null!;

    public History History { get; }

    protected Game Game { get; }

    protected InputService InputService { get; }

    protected RenderingService RenderingService { get; }

    protected ResourceService ResourceService { get; }

    protected IContentManager ContentManager { get; }

    protected StatusService StatusService { get; }

    protected AppStorageService StorageService { get; }

    private readonly ContentService _contentService;

    protected EditorComponent(Game game, string title)
    {
        Game = game;
        Title = title;
        History = new History();
        InputService = game.GetService<InputService>();
        RenderingService = game.GetService<RenderingService>();
        ResourceService = game.GetService<ResourceService>();
        StatusService = game.GetService<StatusService>();
        StorageService = game.GetService<AppStorageService>();
        _contentService = game.GetService<ContentService>();
        ContentManager = _contentService.Get(this);
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
        History.Dispose();
        _contentService.Unload(this);
    }

    public override string ToString()
    {
        return $"{GetType().Name} ({Title})";
    }
}