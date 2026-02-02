using FezEditor.Components;
using FezEditor.Services;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Serilog;

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
        Environment.SetEnvironmentVariable("FNA3D_FORCE_DRIVER", "OpenGL");
        try
        {
            using var editor = new FezEditor();
            editor.Run();
        }
        catch (Exception e)
        {
            Logger.Fatal(e, "Unhandled Exception");
        }
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
        // Services
        {
            this.AddService(_imGuiService = new ImGuiService(this));
        }
        
        // Components
        {
            this.AddComponent(new TestComponent(this));
            this.AddComponent(new ContentExtractor(this));
        }
        
        ImGuiX.ImGuiService = _imGuiService;
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
        base.Dispose(disposing);
        this.RemoveServices();
    }
}