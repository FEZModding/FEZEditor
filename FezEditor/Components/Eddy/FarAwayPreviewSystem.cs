using FezEditor.Actors;
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
            Eddy.Visuals = EddyVisuals.Preview;
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

    private EddyVisuals _savedVisuals;

    private (int W, int H) _savedRtSize;

    private State _state = State.Closed;

    private ExportKind _pendingExport;

    private bool _closeRequested;

    public FarAwayPreviewSystem(Scene scene)
    {
        _scene = scene;
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
            _state = State.Capturing;
            return;
        }

        if (_state == State.Capturing)
        {
            Capture();
            _state = State.Idle;
            RestoreRt();
            Eddy.Visuals = EddyVisuals.Preview;
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

        _savedRtSize = _scene.Viewport.GetSize();
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
    }

    private void RestoreRt()
    {
        _scene.Viewport.SetClearColor(Color.Black);
        _scene.Viewport.SetSize(_savedRtSize.W, _savedRtSize.H);
    }

    private void Close()
    {
        RestoreRt();
        Eddy.Visuals = _savedVisuals;
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

        string outputPath;
        if (_pendingExport == ExportKind.FarawayThumb)
        {
            var dir = Resources.GetFullPath("Other Textures/faraway_thumbs");
            Directory.CreateDirectory(dir);
            outputPath = Path.Combine(dir, $"{Level.Name} ({Eddy.CurrentView}).png");
        }
        else
        {
            var dir = Resources.GetFullPath("Other Textures/map_screens");
            Directory.CreateDirectory(dir);
            outputPath = Path.Combine(dir, $"{Level.Name}.png");
        }

        _ = Task.Run(() =>
        {
            try
            {
                var rgba = new byte[texture.Width * texture.Height * 4];
                for (var i = 0; i < pixels.Length; i++)
                {
                    var c = pixels[i];
                    if (_pendingExport == ExportKind.FarawayThumb && c is { R: > 200, G: < 50, B: > 200 })
                    {
                        rgba[(i * 4) + 0] = 0;
                        rgba[(i * 4) + 1] = 0;
                        rgba[(i * 4) + 2] = 0;
                        rgba[(i * 4) + 3] = 0;
                    }
                    else
                    {
                        rgba[(i * 4) + 0] = c.R;
                        rgba[(i * 4) + 1] = c.G;
                        rgba[(i * 4) + 2] = c.B;
                        rgba[(i * 4) + 3] = 255;
                    }
                }

                using var image = Image.LoadPixelData<Rgba32>(rgba, texture.Width, texture.Height);
                image.SaveAsPng(outputPath);

                Logger.Information("Saved {0}", outputPath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save capture");
            }
        });
    }

    private enum State
    {
        Idle,
        WaitFrame,
        Capturing,
        Closed
    }

    private enum ExportKind
    {
        FarawayThumb,
        MapScreen
    }
}