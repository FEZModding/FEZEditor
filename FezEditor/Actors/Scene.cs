using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class Scene : IDisposable
{
    private readonly Game _game;

    private readonly Rid _rootRid;

    private readonly Rid _worldRid;

    private readonly Rid _rtRid;

    private readonly RenderingService _rendering;
    
    private readonly Dictionary<int, Actor> _actors = new();

    private int _nextId = -1;
    
    private bool _disposed;

    public Scene(Game game)
    {
        #region Main services

        _game = game;
        _rendering = game.GetService<RenderingService>();

        #endregion

        #region World setup

        _worldRid = _rendering.WorldCreate();
        _rtRid = _rendering.RenderTargetCreate();
        _rootRid = _rendering.WorldGetRoot(_worldRid);
        _rendering.RenderTargetSetWorld(_rtRid, _worldRid);

        #endregion
    }

    public Actor CreateActor(int id = -1)
    {
        id = id == -1 ? _nextId++ : id;
        var actor = new Actor(_game, _rootRid, id);
        _actors.Add(id, actor);
        return actor;
    }

    public Actor? FindActor(int id)
    {
        return _actors.GetValueOrDefault(id);
    }

    public void DestroyActor(int id)
    {
        if (_actors.Remove(id, out var actor))
        {
            actor.Dispose();
        }
    }

    public void Update(GameTime gameTime)
    {
        foreach (var actor in _actors.Values)
        {
            actor.Update(gameTime);
        }
    }

    public Texture2D? GetViewportTexture()
    {
        return _rendering.RenderTargetGetTexture(_rtRid);
    }

    public void SetViewportSize(int width, int height)
    {
        _rendering.RenderTargetSetSize(_rtRid, width, height);
    }

    public void SetViewportAspectRatio(float ratio)
    {
        var camera = _actors.Values
            .FirstOrDefault(a => a.HasComponent<Camera>())?
            .GetComponent<Camera>();
        
        if (camera != null)
        {
            camera.AspectRatio = ratio;
        }
    }
    
    public virtual void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        
        _disposed = true;
        foreach (var actor in _actors.Values)
        {
            actor.Dispose();
        }

        _actors.Clear();
        _rendering.FreeRid(_rtRid);
        _rendering.FreeRid(_worldRid);
    }
}