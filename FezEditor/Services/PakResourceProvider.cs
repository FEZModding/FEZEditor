using FEZRepacker.Core.FileSystem;
using FEZRepacker.Core.XNB;

namespace FezEditor.Services;

internal class PakResourceProvider : IResourceProvider
{
    public bool IsReadonly => true;

    public string Root => _pakFile.Name;

    public IEnumerable<string> Files => _records.Keys;

    private readonly Dictionary<string, string> _records = new(StringComparer.OrdinalIgnoreCase);
    
    private readonly FileInfo _pakFile;

    public PakResourceProvider(FileInfo info)
    {
        if (info is not  { Extension: ".pak", Exists: true })
        {
            throw new FileNotFoundException(info.FullName);
        }

        _pakFile = info;
        Refresh();
    }

    public bool Exists(string path)
    {
        return _records.ContainsKey(path);
    }

    public string GetExtension(string path)
    {
        return _records.GetValueOrDefault(path, "");
    }

    public string GetFullPath(string path)
    {
        return _records.TryGetValue(path, out var extension)
            ? Path.Combine(_pakFile.Name, path + extension)
            : "";
    }

    public T Load<T>(string path) where T : class
    {
        if (!Exists(path))
        {
            throw new FileNotFoundException(path);
        }
        
        using var stream = _pakFile.OpenRead();
        using var reader = new PakReader(stream);
        
        var record = reader.ReadFiles().FirstOrDefault(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (record == null)
        {
            throw new FileNotFoundException(path);
        }

        using var xnbStream = record.Open();
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

    public void Save<T>(string path, T asset) where T : class
    {
        throw new NotSupportedException();
    }

    public void Refresh()
    {
        using var stream = _pakFile.OpenRead();
        using var reader = new PakReader(stream);

        _records.Clear();
        foreach (var record in reader.ReadFiles())
        {
            var normalizedPath = record.Path.Replace('\\', '/');
            _records[normalizedPath] = record.FindExtension();
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _records.Clear();
    }
}