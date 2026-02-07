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

    public EditorComponent? ActiveEditor { get; private set; }
    
    public event Action<IResourceService?>? ResourcesChanged;

    private readonly List<EditorComponent> _editors = new();

    private readonly Game _game;
    
    public EditorService(Game game)
    {
        _game = game;
    }

    public void OpenEditor(EditorComponent editor)
    {
        _editors.Add(editor);
        {
            editor.Initialize();
        }
        
        if (ActiveEditor != editor)
        {
            ActiveEditor = editor;
        }
    }

    public void CloseEditor(EditorComponent editor)
    {
        if (_editors.Remove(editor))
        {
            editor.Dispose();
        }
        
        if (editor == ActiveEditor)
        {
            ActiveEditor = Editors.FirstOrDefault();
        }
    }

    public void MarkEditorActive(EditorComponent editor)
    {
        if (Editors.Contains(editor) && ActiveEditor != editor) 
        {
            ActiveEditor = editor;
        }
    }

    public void OpenResources(IResourceService service) 
    {
        CloseResources();
        _game.Services.AddService(typeof(IResourceService), service);
        ResourcesChanged?.Invoke(service);
    }

    public void CloseResources()
    {
        _game.RemoveService<IResourceService>();
        ResourcesChanged?.Invoke(null);
    }
}