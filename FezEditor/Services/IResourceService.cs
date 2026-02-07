using FezEditor.Structure;

namespace FezEditor.Services;

public interface IResourceService : IDisposable
{
    IResourceProvider? Provider { get; }

    event Action? ProviderOpened;

    event Action? ProviderClosed;

    void OpenProvider(FileSystemInfo info);

    void CloseProvider();
}