using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class SceneViewport : IDisposable
{
    private readonly RenderingService _rendering;

    private readonly Rid _rt;

    internal SceneViewport(Game game, Rid worldRid)
    {
        _rendering = game.GetService<RenderingService>();
        _rt = _rendering.RenderTargetCreate();
        _rendering.RenderTargetSetWorld(_rt, worldRid);
        _rendering.RenderTargetSetClearColor(_rt, Color.Black);
    }
    
    public Texture2D? GetTexture()
    {
        return _rendering.RenderTargetGetTexture(_rt);
    }

    public void SetSize(int width, int height)
    {
        _rendering.RenderTargetSetSize(_rt, width, height);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_rt);
    }
}