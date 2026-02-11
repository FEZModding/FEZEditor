using FezEditor.Components;
using FezEditor.Structure;
using Microsoft.Xna.Framework;

namespace FezEditor.Services;

public interface IEditorService
{
    EditorFlags Flags { get; }
    
    IEnumerable<EditorComponent> Editors { get; }
    
    void OpenEditor(EditorComponent editor);

    void CloseEditor(EditorComponent editor);
    
    void CloseActiveEditor();
    
    void CloseAllEditors();

    void FlushPendingCloses();

    void MarkEditorActive(EditorComponent editor);
    
    void UpdateActiveEditor(GameTime gameTime);

    void UndoActiveEditorChanges();
    
    void RedoActiveEditorChanges();

    bool HasEditorUnsavedChanges();
}