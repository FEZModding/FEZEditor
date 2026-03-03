using FezEditor.Tools;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;

namespace FezEditor.Services;

[UsedImplicitly]
public class ContentService : IDisposable
{
    private const string Root = "Content";

    private readonly Dictionary<object, IContentManager> _managers = new();

    private readonly IServiceProvider _services;

    public IContentManager Global { get; }

    public ContentService(Game game)
    {
        _services = game.Services;
        Global = Get(game);
    }

    public IContentManager Get<T>(T context) where T : class
    {
        if (!_managers.TryGetValue(context, out var manager))
        {
            if (FezEditor.IsDebugBuild)
            {
                manager = new DirContentManager(_services, Root);
            }
            else
            {
                manager = new ZipContentManager(_services, Path.ChangeExtension(Root, ".pkz"));
            }

            _managers.Add(context, manager);
        }

        return manager;
    }

    public void Unload<T>(T context) where T : class
    {
        if (_managers.Remove(context, out var manager))
        {
            manager.Unload();
            manager.Dispose();
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var cm in _managers.Values)
        {
            cm.Unload();
            cm.Dispose();
        }

        _managers.Clear();
    }
}