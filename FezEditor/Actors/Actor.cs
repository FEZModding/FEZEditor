using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class Actor : IDisposable
{
    public Transform Transform { get; private set; }

    public bool Active { get; set; } = true;
    
    public Rid InstanceRid { get; private set; }
    
    private readonly List<ActorComponent> _components = new();

    private readonly int _id;

    private readonly Game _game;

    private readonly RenderingService _rendering;
    
    private bool _disposed;

    public Actor(Game game, Rid parentRid, int id = -1)
    {
        _id = id;
        _game = game;
        _rendering = game.GetService<RenderingService>();
        InstanceRid = _rendering.InstanceCreate(parentRid);
        Transform = AddComponent<Transform>();
    }
    
    public bool HasComponent<T>() where T : ActorComponent
    {
        return _components.OfType<T>().Any();
    }
    
    public T AddComponent<T>() where T : ActorComponent, new()
    {
        if (HasComponent<T>())
        {
            throw new InvalidOperationException($"Actor '{_id}' already has component {typeof(T).Name}");   
        }
        
        var component = new T();
        component.Initialize(_game, this);
        _components.Add(component);
        component.Initialize();
        return component;
    }
    
    public T GetComponent<T>() where T : ActorComponent
    {
        return TryGetComponent<T>(out var component)
            ? component!
            : throw new InvalidOperationException($"Actor '{_id}' has no component {typeof(T).Name}");
    }
    
    public bool TryGetComponent<T>(out T? component) where T : ActorComponent
    {
        component = _components.OfType<T>().FirstOrDefault();
        return component is not null;
    }
    
    public bool RemoveComponent<T>() where T : ActorComponent
    {
        if (typeof(T) == typeof(Transform))
        {
            throw new InvalidOperationException("Cannot remove the Transform component.");
        }

        var component = _components.OfType<T>().FirstOrDefault();
        if (component is null)
        {
            return false;
        }

        component.Dispose();
        return _components.Remove(component);
    }

    public void Update(GameTime gameTime)
    {
        if (!Active)
        {
            return;
        }
        
        foreach (var component in _components)
        {
            if (component.Enabled)
            {
                component.Update(gameTime);
            }
        }
    }
    
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        
        _disposed = true;
        foreach (var component in _components)
        {
            component.Dispose();
        }

        _rendering.FreeRid(InstanceRid);
        _components.Clear();
    }
}