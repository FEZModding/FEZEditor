namespace FezEditor.Tools;

public interface IContentManager : IDisposable
{
    T Load<T>(string assetName);

    T LoadJson<T>(string assetName);
    
    byte[] LoadBytes(string assetName);
    
    Stream LoadStream(string assetName);
    
    void Unload();
}