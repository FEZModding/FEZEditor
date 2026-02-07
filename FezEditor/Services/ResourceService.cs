using FezEditor.Structure;
using JetBrains.Annotations;

namespace FezEditor.Services;

[UsedImplicitly]
public class ResourceService : IResourceService
{
    public IResourceProvider? Provider { get; private set; }
    
    public event Action? ProviderOpened;
    
    public event Action? ProviderClosed;
    
    public void OpenProvider(FileSystemInfo info)
    {
        IResourceProvider provider = info switch
        {
            FileInfo file => new PakResourceProvider(file),
            DirectoryInfo dir => new DirResourceProvider(dir),
            _ => throw new ArgumentException("Not supported: " + info)
        };
        
        CloseProvider();
        Provider = provider;
        ProviderOpened?.Invoke();
    }

    public void CloseProvider()
    {
        ProviderClosed?.Invoke();
        Provider?.Dispose();
        Provider = null;
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Provider?.Dispose();
    }
}