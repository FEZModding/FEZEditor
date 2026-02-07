using FezEditor.Components;
using FezEditor.Structure;

namespace FezEditor.Services;

public interface IEditorService
{
    EditorFlags Flags { get; }
    
    IEnumerable<EditorComponent> Editors { get; }
    
    public EditorComponent? ActiveEditor { get; } 
    
    void OpenEditor(EditorComponent editor);

    void CloseEditor(EditorComponent editor);
    
    void MarkEditorActive(EditorComponent editor);

    public event Action<IResourceService?> ResourcesChanged;

    void OpenResources(IResourceService service);
    
    void CloseResources();
}