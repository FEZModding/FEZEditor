using FezEditor.Components;
using FezEditor.Scripting;
using FezEditor.Services;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using SDL3;
using Serilog;

namespace FezEditor;

public class FezEditor : Game
{
    private static ILogger Logger => Logging.Create<FezEditor>();

    public static GraphicsDeviceManager DeviceManager { get; private set; } = null!;

    public static GameWindow GameWindow { get; private set; } = null!;

    public static readonly string Version;

    public static readonly string Authors;

    public static readonly string SplashAuthors;

    public const string Commit = ThisAssembly.Git.Commit;

    private ContentService _content = null!;

    private ImGuiService _imGui = null!;

    private RenderingService _rendering = null!;

    private InputService _input = null!;

    private EditorService _editor = null!;

    private static int Main(string[] args)
    {
        Args.Parse(args);
        Logging.Initialize();
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        Environment.SetEnvironmentVariable("FNA3D_FORCE_DRIVER", "OpenGL");
        Environment.SetEnvironmentVariable("FNA_GRAPHICS_ENABLE_HIGHDPI", "1");
        try
        {
            using var editor = new FezEditor();
            editor.Run();
        }
        catch (Exception e)
        {
            Logger.Fatal(e, "Unhandled Exception");
            return 1;
        }
        finally
        {
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            Log.CloseAndFlush();
        }

        return 0;
    }

    private FezEditor()
    {
        DeviceManager = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
            IsFullScreen = false,
            SynchronizeWithVerticalRetrace = true
        };
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        GameWindow = Window;
    }

    protected override void Initialize()
    {
        Logger.Information("Version - {0} ({1})", Version, Commit);
        Logger.Information("Scripts - {0} entities", ScriptingApi.Entries.Length); // inits collection

        this.CreateService<AppStorageService>();
        _content = this.CreateService<ContentService>();
        _input = this.CreateService<InputService>();
        _imGui = this.CreateService<ImGuiService>();
        _rendering = this.CreateService<RenderingService>();
        this.CreateService<ResourceService>();
        this.CreateService<StatusService>();
        _editor = this.CreateService<EditorService>();
        this.CreateService<HatLaunchService>();
        Content = (ContentManager)_content.Global;

        this.AddComponent(new TitleBar(this));
        this.AddComponent(new MenuBar(this));
        this.AddComponent(new FileBrowser(this));
        this.AddComponent(new StatusBar(this));
        this.AddComponent(new MainLayout(this));
        _editor.OpenEditor(new WelcomeSplash(this));
        base.Initialize();
    }

    protected override void Update(GameTime gameTime)
    {
        SyncBackBuffer();
        _input.Update();
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _rendering.Draw(gameTime);
        _imGui.BeforeLayout(gameTime);
        base.Draw(gameTime);
        _imGui.AfterLayout();
    }

    protected override void Dispose(bool disposing)
    {
        this.RemoveServices();
        base.Dispose(disposing);
    }

    private void SyncBackBuffer()
    {
        // FNA can miss the initial resize when the window launches unfocused
        if (SDL.SDL_GetWindowSizeInPixels(Window.Handle, out var width, out var height) &&
            width > 0 && height > 0)
        {
            var presentation = GraphicsDevice.PresentationParameters;
            if (presentation.BackBufferWidth != width || presentation.BackBufferHeight != height)
            {
                DeviceManager.PreferredBackBufferWidth = width;
                DeviceManager.PreferredBackBufferHeight = height;
                DeviceManager.ApplyChanges();
            }
        }
    }

    static FezEditor()
    {
        // ReSharper disable once HeuristicUnreachableCode
        Version = ThisAssembly.Git.BaseVersion.Major + "." +
                  ThisAssembly.Git.BaseVersion.Minor +
                  (ThisAssembly.Git.BaseVersion.Patch != "0" ? "." + ThisAssembly.Git.BaseVersion.Patch : "");

        var assembly = typeof(FezEditor).Assembly;
        var attrs = assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyCompanyAttribute), false);
        var metadata = assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false);

        Authors = attrs.Length > 0
            ? ((System.Reflection.AssemblyCompanyAttribute)attrs[0]).Company
            : string.Empty;

        SplashAuthors = metadata.OfType<System.Reflection.AssemblyMetadataAttribute>()
            .First(attr => attr.Key == "SplashAuthors").Value ?? string.Empty;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception exception)
        {
            Logger.Fatal(exception, "Unhandled Exception (terminating: {IsTerminating})", args.IsTerminating);
        }
        else
        {
            Logger.Fatal("Unhandled exception object: {@ExceptionObject} (terminating: {IsTerminating})",
                args.ExceptionObject, args.IsTerminating);
        }

        Log.CloseAndFlush();
    }
}