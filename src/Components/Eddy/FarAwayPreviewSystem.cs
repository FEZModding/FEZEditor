using FezEditor.Actors;
using FezEditor.Services;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Microsoft.Xna.Framework.Color;

namespace FezEditor.Components.Eddy;

public class FarAwayPreviewSystem : EddySystem
{
    private static readonly ILogger Logger = Logging.Create<FarAwayPreviewSystem>();

    public bool IsOpen => _state != State.Closed;

    public bool IsExporting => _state == State.WaitFrame;

    public void SetOpen(bool open)
    {
        if (open)
        {
            if (_state != State.Closed)
            {
                return;
            }

            _savedVisuals = Eddy.Visuals;
            _savedRtSize = _scene.Viewport.GetSize();
            Eddy.SwitchToOrtho(ViewMode.Front, 0f);
            _closeRequested = false;
            _state = State.Idle;
        }
        else if (_state != State.Closed)
        {
            RequestClose();
        }
    }

    private readonly Scene _scene;

    private readonly EditorService _editors;

    private EddyVisuals _savedVisuals;

    private (int W, int H) _savedRtSize;

    private State _state = State.Closed;

    private ExportKind _pendingExport;

    private bool _closeRequested;

    public FarAwayPreviewSystem(Scene scene, EditorService editors)
    {
        _scene = scene;
        _editors = editors;
    }

    public override void Update()
    {
        if (_state == State.Idle && _closeRequested)
        {
            Close();
        }
    }

    public void BeforeDraw()
    {
        if (_state == State.Idle && _closeRequested)
        {
            Close();
            return;
        }

        if (_state == State.WaitFrame)
        {
            Capture();
            if (_closeRequested)
            {
                Close();
            }
        }
    }

    public override void Draw()
    {
        if (_state == State.Closed)
        {
            return;
        }

        var open = true;

        const ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;
        if (ImGui.Begin($"Level Previewer##{Level.Name}", ref open, flags))
        {
            if (_state == State.Idle)
            {
                ImGui.Text("Use \"View Options\" button to change orthogonal view");
                ImGui.Separator();

                if (ImGui.Button("Save as Faraway Thumb"))
                {
                    BeginExport(ExportKind.FarawayThumb);
                }

                ImGui.SameLine();

                if (ImGui.Button("Save as Map Screen"))
                {
                    BeginExport(ExportKind.MapScreen);
                }
            }
            else
            {
                ImGui.TextDisabled("Capturing...");
            }

            ImGui.End();
        }

        if (!open)
        {
            RequestClose();
        }
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_state != State.Closed)
        {
            Close();
        }
    }

    private void BeginExport(ExportKind kind)
    {
        _pendingExport = kind;
        _state = State.WaitFrame;

        if (kind == ExportKind.FarawayThumb)
        {
            _scene.Viewport.SetSize(512, 512);
            _scene.Viewport.SetClearColor(new Color(255, 0, 255)); // magenta chroma-key
            Eddy.Visuals = EddyVisuals.Preview & ~EddyVisuals.Sky;
        }
        else
        {
            _scene.Viewport.SetSize(128, 128);
            _scene.Viewport.SetClearColor(Color.Black);
            Eddy.Visuals = EddyVisuals.Preview;
        }

        Eddy.VisualizeAll();
    }

    private void Close()
    {
        Eddy.SwitchToPerspective();
        _closeRequested = false;
        _state = State.Closed;
    }

    private void RequestClose()
    {
        if (_state == State.Idle)
        {
            Close();
            return;
        }

        _closeRequested = true;
    }

    private void Capture()
    {
        if (_scene.Viewport.GetTexture() is not RenderTarget2D texture)
        {
            Logger.Warning("Render target texture is null, skipping capture");
            return;
        }

        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);

        var assetPath = _pendingExport == ExportKind.FarawayThumb
            ? $"Other Textures/faraway_thumbs/{Level.Name} ({Eddy.CurrentView})"
            : $"Other Textures/map_screens/{Level.Name}";

        var outputPath = Resources.GetFullPath(assetPath);
        if (!Path.HasExtension(outputPath))
        {
            outputPath += ".png";
        }

        var dir = Path.GetDirectoryName(outputPath)!;
        Directory.CreateDirectory(dir);

        var rgba = new byte[texture.Width * texture.Height * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            var color = pixels[i];
            var chromaKey = _pendingExport == ExportKind.FarawayThumb && color is { R: > 200, G: < 50, B: > 200 };
            rgba[(i * 4) + 0] = chromaKey ? (byte)0 : color.R;
            rgba[(i * 4) + 1] = chromaKey ? (byte)0 : color.G;
            rgba[(i * 4) + 2] = chromaKey ? (byte)0 : color.B;
            rgba[(i * 4) + 3] = chromaKey ? (byte)0 : color.A;
        }

        try
        {
            using var image = Image.LoadPixelData<Rgba32>(rgba, texture.Width, texture.Height);
            image.SaveAsPng(outputPath);
            Logger.Information("Saved {0}", assetPath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to save capture");
        }

        _scene.Viewport.SetClearColor(Color.Black);
        _scene.Viewport.SetSize(_savedRtSize.W, _savedRtSize.H);
        Eddy.Visuals = _savedVisuals;
        Eddy.VisualizeAll();

        Resources.Refresh();
        _editors.OpenEditorFor(assetPath);
        _state = State.Idle;
    }

    private enum State
    {
        Idle,
        WaitFrame,
        Closed
    }

    private enum ExportKind
    {
        FarawayThumb,
        MapScreen
    }
}