using JetBrains.Annotations;
using Microsoft.Xna.Framework;

namespace FezEditor.Services;

[UsedImplicitly]
public class ResourceService : IDisposable
{
    public event Action? ProviderChanged;

    public bool HasNoProvider => _provider == null;

    public bool IsReadonly => _provider?.IsReadonly ?? true;

    public string Root => _provider?.Root ?? string.Empty;

    public IEnumerable<string> Files => _provider?.Files ?? Enumerable.Empty<string>();

    private IResourceProvider? _provider;

    private readonly Game _game;

    public ResourceService(Game game)
    {
        _game = game;
        _game.Activated += OnGameActivated;
    }

    private void OnGameActivated(object? o, EventArgs eventArgs)
    {
#if (!DEBUG)
        if (_provider != null)
        {
            _provider.Refresh();
            ProviderChanged?.Invoke();
        }
#endif
    }

    public void OpenProvider(FileSystemInfo info)
    {
        IResourceProvider provider = info switch
        {
            FileInfo file => new PakResourceProvider(file),
            DirectoryInfo dir => new DirResourceProvider(dir),
            _ => throw new ArgumentException("Not supported: " + info)
        };

        CloseProvider();
        _provider = provider;
        ProviderChanged?.Invoke();
    }

    public void CloseProvider()
    {
        ProviderChanged?.Invoke();
        _provider?.Dispose();
        _provider = null;
    }

    public object Load(string path)
    {
        return _provider!.Load<object>(path);
    }

    public void Save(string path, object asset)
    {
        _provider!.Save(path, asset);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _provider?.Dispose();
        _game.Activated -= OnGameActivated;
    }
}