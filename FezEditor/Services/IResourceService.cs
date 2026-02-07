namespace FezEditor.Services;

public interface IResourceService : IDisposable
{
    bool IsReadonly { get; }
    
    string Root { get; }
    
    IEnumerable<string> Files { get; }
    
    event Action? Refreshed;
    
    event Action? Disposed;

    bool Exists(string path);
    
    string GetFullPath(string path);
    
    string GetExtension(string path);
    
    T Load<T>(string path) where T : class;
    
    void Refresh();
}