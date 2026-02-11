using FezEditor.Hosts;
using FezEditor.Services;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class TestComponent : EditorComponent
{
    private readonly GeometryHost _host;
    
    public TestComponent(Game game, string title) : base(game, title)
    {
        var resourceService = game.GetService<IResourceService>();
        var artObject = resourceService.Provider!.Load<object>("art objects/big_treeao");
        _host = new GeometryHost(game);
        _host.Load(artObject);
    }

    public override void Update(GameTime gameTime)
    {
        _host.Update(gameTime);
    }

    public override void Draw()
    {
        var size = ImGuiX.GetContentRegionAvail();
        var w = (int)size.X;
        var h = (int)size.Y;
            
        if (w > 0 && h > 0)
        {
            var texture = _host.GetViewportTexture();
            if (texture == null || texture.Width != w || texture.Height != h)
            {
                _host.SetViewportSize(w, h);
                _host.Camera.AspectRatio = (float)w / h;
            }

            if (texture is { IsDisposed: false })
            {
                ImGuiX.Image(texture, size);
            }
        }
    }

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }
}