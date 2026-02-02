using FezEditor.Components;
using FezEditor.Services;
using Microsoft.Xna.Framework;
using Serilog;
using Serilog.Core;

namespace FezEditor;

public class FezEditor : Game
{
    private static readonly ILogger Logger = Logging.Create<FezEditor>();
    
    private readonly GraphicsDeviceManager _deviceManager;

    private ImGuiService? _imGuiService;
    
    [STAThread]
    private static void Main(string[] args)
    {
        Logging.Initialize();
        using var editor = new FezEditor();
        editor.Run();
    }
    
    private FezEditor()
    {
        _deviceManager = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            IsFullScreen = false,
            SynchronizeWithVerticalRetrace = true,
        };
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
    }

    protected override void Initialize()
    {
        Services.AddService(typeof(ImGuiService), _imGuiService = new ImGuiService(this));
        
        Components.Add(new TestComponent(this));
        
        base.Initialize();
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(0.2f, 0.2f, 0.294f));
        
        _imGuiService?.BeforeLayout(gameTime);
        
        base.Draw(gameTime);
        
        _imGuiService?.AfterLayout();
    }

    protected override void Dispose(bool disposing)
    {
        _imGuiService?.Dispose();
        
        base.Dispose(disposing);
    }
}