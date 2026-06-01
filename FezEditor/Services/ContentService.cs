using FezEditor.Tools;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Serilog;

namespace FezEditor.Services;

[UsedImplicitly]
public class ContentService : IDisposable
{
    private static readonly ILogger Logger = Logging.Create<ContentService>();

    private const string Root = "Content";

    private const string Bundle = "ContentBundle";

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
#if DEBUG
            manager = new DirContentManager(_services, Root);
#else
            var assembly = typeof(FezEditor).Assembly;
            var stream = assembly.GetManifestResourceStream(Bundle)
                         ?? throw new FileNotFoundException($"Embedded content resource not found: {Bundle}!");
            manager = new ZipContentManager(_services, stream);
#endif

            Logger.Information("Loaded {0} for {1}",
                manager.GetType().Name, context.GetType().Name);
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
            Logger.Information("Unloaded {0} for {1}",
                manager.GetType().Name, context.GetType().Name);
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