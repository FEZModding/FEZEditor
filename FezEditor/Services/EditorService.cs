using FezEditor.Components;
using FezEditor.Structure;
using FezEditor.Tools;
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

    private readonly List<EditorComponent> _editors = new();

    private readonly List<EditorComponent> _pendingClose = new();
    
    private readonly IInputService _inputService;
    
    private EditorComponent? _activeEditor;

    public EditorService(Game game)
    {
        _inputService = game.GetService<IInputService>();
    }

    public void OpenEditor(EditorComponent editor)
    {
        if (_editors.All(e => e.Title != editor.Title))
        {
            _editors.Add(editor);
            _activeEditor = editor;
            _activeEditor.History.StateChanged += UpdateHistoryFlags;
            UpdateFlags();
        }
    }

    public void CloseEditor(EditorComponent editor)
    {
        _pendingClose.Add(editor);
    }

    public void CloseActiveEditor()
    {
        _pendingClose.Add(_activeEditor!);
    }

    public void CloseAllEditors()
    {
        _pendingClose.AddRange(_editors);
    }

    public void MarkEditorActive(EditorComponent editor)
    {
        _activeEditor = editor;
    }

    public void UpdateActiveEditor(GameTime gameTime)
    {
        if (_activeEditor != null)
        {
            _activeEditor.Update(gameTime);
            if (_inputService.IsActionPressed(InputActions.UiUndo))
            {
                _activeEditor.History.Undo();
            }

            if (_inputService.IsActionPressed(InputActions.UiRedo))
            {
                _activeEditor.History.Redo();
            }
        }
    }

    public void UndoActiveEditorChanges()
    {
        _activeEditor!.History.Undo();
    }

    public void RedoActiveEditorChanges()
    {
        _activeEditor!.History.Redo();
    }
    
    public bool HasEditorUnsavedChanges()
    {
        // TODO: implement this with saving
        return _activeEditor!.History.UndoCount > 0;
    }

    public void FlushPendingCloses()
    {
        if (_pendingClose.Count == 0)
        {
            return;
        }

        _activeEditor!.History.StateChanged -= UpdateHistoryFlags;
        foreach (var editor in _pendingClose)
        {
            if (_editors.Remove(editor))
            {
                editor.Dispose();
            }

            if (editor == _activeEditor)
            {
                _activeEditor = _editors.Count > 0 ? _editors[^1] : null;
                if (_activeEditor != null)
                {
                    _activeEditor.History.StateChanged += UpdateHistoryFlags;
                }
                UpdateFlags();
            }
        }

        _pendingClose.Clear();
    }
    
    private void UpdateFlags()
    {
        if (_activeEditor is WelcomeComponent)
        {
            Flags &= ~(EditorFlags.CloseFile | EditorFlags.QuitToWelcome);
        }
        else
        {
            Flags |= EditorFlags.QuitToWelcome;
            if (Editors.Any())
            {
                Flags |= EditorFlags.CloseFile;
            }
            else
            {
                Flags &= ~EditorFlags.CloseFile;
            }
        }
    }
    
    private void UpdateHistoryFlags()
    {
        if (_activeEditor!.History.CanUndo)
        {
            Flags |= EditorFlags.Undo;
        }
        else
        {
            Flags &= ~EditorFlags.Undo;
        }

        if (_activeEditor.History.CanRedo)
        {
            Flags |= EditorFlags.Redo;
        }
        else
        {
            Flags &= ~EditorFlags.Redo;
        }
    }
}