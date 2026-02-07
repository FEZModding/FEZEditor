using FezEditor.Tools;
using FEZRepacker.Core.Conversion;
using FEZRepacker.Core.FileSystem;
using FEZRepacker.Core.XNB;
using JetBrains.Annotations;

namespace FezEditor.Services;

[UsedImplicitly]
public class DirResourceService : IResourceService
{
    public bool IsReadonly => false;
    
    public string Root => _directory.Name;

    public IEnumerable<string> Files => _files.Keys;
    
    public event Action? Refreshed;
    public event Action? Disposed;

    private readonly Dictionary<string, FileInfo> _files = new(StringComparer.OrdinalIgnoreCase);

    private readonly DirectoryInfo _directory;

    public DirResourceService(DirectoryInfo info)
    {
        if (info is not { Exists: true })
        {
            throw new DirectoryNotFoundException(info.FullName);
        }

        _directory = info;
        Refresh();
    }

    public bool Exists(string path)
    {
        return _files.ContainsKey(path);
    }

    public string GetExtension(string path)
    {
        return _files.GetValueOrDefault(path)?.FullName.GetExtension() ?? "";
    }

    public string GetFullPath(string path)
    {
        return _files.GetValueOrDefault(path)?.FullName ?? "";
    }

    public T Load<T>(string path) where T : class
    {
        var info = _files.GetValueOrDefault(path);
        if (info is not { Exists: true })
        {
            throw new FileNotFoundException(path);
        }

        if (info.Extension == ".xnb")
        {
            using var xnbStream = info.Open(FileMode.Open);
            var initialPosition = xnbStream.Position;
            try
            {
                return (T)XnbSerializer.Deserialize(xnbStream)!;
            }
            catch
            {
                xnbStream.Seek(initialPosition, SeekOrigin.Begin);
                throw;
            }
        }

        var bundles = FileBundle.BundleFilesAtPath(info.FullName);
        if (bundles.Count == 0)
        {
            throw new FileNotFoundException(info.FullName);
        }

        return (T)FormatConversion.Deconvert(bundles.First())!;
    }

    public void Refresh()
    {
        _files.Clear();
        foreach (var file in _directory.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            var path = file.FullName.WithoutBaseDirectory(_directory.FullName);
            var normalizedPath = path.Replace(path.GetExtension(), "").Replace('\\', '/');
            _files[normalizedPath] = file;
        }
        Refreshed?.Invoke();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _files.Clear();
        Disposed?.Invoke();
    }
}