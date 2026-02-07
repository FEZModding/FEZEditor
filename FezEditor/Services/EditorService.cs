using FezEditor.Components;
using FezEditor.Structure;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Serilog;

namespace FezEditor.Services;

[UsedImplicitly]
public class EditorService : IEditorService
{
    private static readonly ILogger Logger = Logging.Create<EditorService>();

    public EditorFlags Flags { get; private set; }

    public IEnumerable<EditorComponent> Editors => _editors;

    public EditorComponent? ActiveEditor { get; private set; }

    private readonly List<EditorComponent> _editors = new();

    private readonly List<EditorComponent> _pendingClose = new();

    public void OpenEditor(EditorComponent editor)
    {
        editor.Initialize();
        _editors.Add(editor);
        ActiveEditor = editor;
    }

    public void CloseEditor(EditorComponent editor)
    {
        _pendingClose.Add(editor);
    }

    public void CloseAllEditors()
    {
        _pendingClose.AddRange(_editors);
    }

    public void MarkEditorActive(EditorComponent editor)
    {
        if (ActiveEditor != editor)
        {
            ActiveEditor = editor;
            Flags = EditorFlags.None;
            if (ActiveEditor is not WelcomeComponent)
            {
                Flags |= EditorFlags.CloseFile | EditorFlags.QuitToWelcome;
            }
        }
    }

    public void FlushPendingCloses()
    {
        if (_pendingClose.Count == 0)
        {
            return;
        }

        foreach (var editor in _pendingClose)
        {
            if (_editors.Remove(editor))
            {
                editor.Dispose();
            }

            if (editor == ActiveEditor)
            {
                ActiveEditor = _editors.Count > 0 ? _editors[^1] : null;
            }
        }

        _pendingClose.Clear();
    }
}