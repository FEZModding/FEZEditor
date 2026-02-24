using System.Reflection;
using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class Actor : IDisposable
{
    public Transform Transform { get; private set; }

    public bool Active { get; set; } = true;
    
    public string Name { get; set; } = "Actor";

    public Rid InstanceRid { get; }

    private readonly List<ActorComponent> _components = new();

    private readonly Game _game;
    
    private readonly IContentManager _content;

    private readonly RenderingService _rendering;

    private bool _disposed;

    internal Actor(Game game, Rid parentRid, IContentManager content)
    {
        _game = game;
        _content = content;
        _rendering = game.GetService<RenderingService>();
        InstanceRid = _rendering.InstanceCreate(parentRid);
        Transform = AddComponent<Transform>();
    }

    public bool HasComponent<T>() where T : ActorComponent
    {
        return _components.OfType<T>().Any();
    }

    public T AddComponent<T>() where T : ActorComponent
    {
        if (HasComponent<T>())
        {
            throw new InvalidOperationException($"Actor already has component {typeof(T).Name}");
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var component = (T)Activator.CreateInstance(typeof(T), flags, null, new object[] { _game, this }, null)!;
        component.LoadContent(_content);
        _components.Add(component);
        return component;
    }

    public T GetComponent<T>() where T : ActorComponent
    {
        return TryGetComponent<T>(out var component)
            ? component!
            : throw new InvalidOperationException($"Actor has no component {typeof(T).Name}");
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
        foreach (var component in _components.Where(c => c.Enabled))
        {
            component.Update(gameTime);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
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

    public override string ToString()
    {
        return $"{{Name: {Name}}}";
    }
}