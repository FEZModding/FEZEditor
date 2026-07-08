using FezEditor.Actors;
using FezEditor.Services;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Microsoft.Xna.Framework.Color;

namespace FezEditor.Components.Eddy;

public class FarAwayPreviewSystem : EddySystem
{
    private static readonly ILogger Logger = Logging.Create<FarAwayPreviewSystem>();

    private static readonly string[] ExportKindLabels = ["Faraway Thumbnail", "Map Screen"];

    private const int FarawayPixelsPerTrile = 8;

    private const int MapScreenSize = 128;

    private const int DefaultMapPixelsPerTrile = 16;

    private readonly Scene _scene;

    private readonly EditorService _editors;

    private readonly Actor _cameraActor;

    private readonly Camera _camera;

    private EddyVisuals _savedVisuals;

    private (int W, int H) _savedRtSize;

    private float _savedCameraSize;

    private Vector3 _savedCameraPosition;

    private ExportKind _selectedExport;

    private ExportKind _pendingExport;

    public FarAwayPreviewSystem(Scene scene, EditorService editors, Actor cameraActor)
    {
        _scene = scene;
        _editors = editors;
        _cameraActor = cameraActor;
        _camera = cameraActor.GetComponent<Camera>();
    }

    public override void Update()
    {
        if (Eddy.PreviewState is { Current: FayAwayPreviewState.Opened, Previous: FayAwayPreviewState.Closed })
        {
            Open();
        }

        if (Eddy.PreviewState is { Current: FayAwayPreviewState.Closed, Previous: FayAwayPreviewState.Opened })
        {
            Close();
        }
    }

    public void BeforeDraw()
    {
        if (Eddy.PreviewState is { Current: FayAwayPreviewState.Opened, Previous: FayAwayPreviewState.Closed })
        {
            Open();
        }

        if (Eddy.PreviewState is { Current: FayAwayPreviewState.Closed, Previous: FayAwayPreviewState.Opened })
        {
            Close();
            return;
        }

        if (Eddy.PreviewState.Current == FayAwayPreviewState.Exporting)
        {
            Capture();
            return;
        }

        if (Eddy.PreviewState is { Current: FayAwayPreviewState.Closed, Previous: FayAwayPreviewState.Exporting })
        {
            Capture();
            Close();
        }
    }

    public override void Draw()
    {
        if (Eddy.PreviewState.Current == FayAwayPreviewState.Closed)
        {
            return;
        }

        if (Eddy.PreviewState is { Current: FayAwayPreviewState.Opened, Previous: FayAwayPreviewState.Closed })
        {
            Open();
        }

        var open = true;

        const ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;
        if (ImGui.Begin($"Level Previewer##{Level.Name}", ref open, flags))
        {
            if (Eddy.PreviewState.Current == FayAwayPreviewState.Opened)
            {
                ImGui.Text("Use \"View Options\" button to change orthogonal view");
                ImGui.Separator();

                var selectedExport = (int)_selectedExport;
                if (ImGui.Combo("Export Kind", ref selectedExport, ExportKindLabels, ExportKindLabels.Length))
                {
                    _selectedExport = (ExportKind)selectedExport;
                }

                ImGui.TextDisabled(GetExportDescription(_selectedExport));
                if (ImGui.Button("Save the preview"))
                {
                    BeginExport(_selectedExport);
                }

                if (_selectedExport == ExportKind.MapScreen)
                {
                    DrawMapScreenOverlay();
                }
                else
                {
                    DrawFarawayThumbOverlay();
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
        if (Eddy.PreviewState is not { Current: FayAwayPreviewState.Closed, Previous: FayAwayPreviewState.Closed })
        {
            Close();
        }
    }

    private void Open()
    {
        if (Eddy.PreviewState is not { Current: FayAwayPreviewState.Opened, Previous: FayAwayPreviewState.Closed })
        {
            return;
        }

        _savedVisuals = Eddy.Visuals;
        _savedRtSize = _scene.Viewport.GetSize();
        if (Eddy.CurrentView == ViewMode.Perspective)
        {
            Eddy.SwitchToOrtho(ViewMode.Front, 0f);
        }

        Eddy.ConsumePreviewStateTransition();
    }

    private void BeginExport(ExportKind kind)
    {
        _pendingExport = kind;
        _savedCameraSize = _camera.Size;
        _savedCameraPosition = _cameraActor.Transform.Position;
        Eddy.SetPreviewState(FayAwayPreviewState.Exporting);

        if (kind == ExportKind.FarawayThumb)
        {
            var (width, height) = ComputeFarawayExportSize();
            _scene.Viewport.SetSize(width, height);
            _cameraActor.Transform.Position = Level.Size.ToXna() / 2f;
            SetCameraSize(height / (float)FarawayPixelsPerTrile, true);
            _scene.Viewport.SetClearColor(new Color(255, 0, 255)); // magenta chroma-key
            Eddy.Visuals = EddyVisuals.Preview & ~EddyVisuals.Sky;
        }
        else
        {
            var pixelsPerTrile = GetMapPixelsPerTrile();
            _scene.Viewport.SetSize(MapScreenSize, MapScreenSize);
            SetCameraSize(MapScreenSize / (float)pixelsPerTrile, false);
            _scene.Viewport.SetClearColor(Color.Black);
            Eddy.Visuals = EddyVisuals.Preview;
        }

        Eddy.VisualizeAll();
    }

    private void Close()
    {
        Eddy.SwitchToPerspective();
        Eddy.SetPreviewState(FayAwayPreviewState.Closed);
        Eddy.ConsumePreviewStateTransition();
    }

    private void RequestClose()
    {
        if (Eddy.PreviewState.Current == FayAwayPreviewState.Opened)
        {
            Close();
            return;
        }

        Eddy.SetPreviewState(FayAwayPreviewState.Closed);
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
        _cameraActor.Transform.Position = _savedCameraPosition;
        RestoreCameraSize();
        Eddy.Visuals = _savedVisuals;
        Eddy.VisualizeAll();

        Resources.Refresh();
        _editors.OpenEditorFor(assetPath);
        Eddy.SetPreviewState(FayAwayPreviewState.Opened);
        Eddy.ConsumePreviewStateTransition();
    }

    private void SetCameraSize(float size, bool overrideMapZoom)
    {
        if (overrideMapZoom && _cameraActor.FindComponent<MapZoomControl>() is { } zoom)
        {
            zoom.SetOverride(size);
        }
        else
        {
            _camera.Size = size;
        }
    }

    private void RestoreCameraSize()
    {
        if (_cameraActor.FindComponent<MapZoomControl>() is { } zoom)
        {
            zoom.ClearOverride();
        }
        else
        {
            _camera.Size = _savedCameraSize;
        }
    }

    private (int Width, int Height) ComputeFarawayExportSize()
    {
        var size = Level.Size.ToXna();
        var (width, height) = Eddy.CurrentView switch
        {
            ViewMode.Right or ViewMode.Left => (MathF.Max(1f, size.Z), MathF.Max(1f, size.Y)),
            _ => (MathF.Max(1f, size.X), MathF.Max(1f, size.Y))
        };

        return (
            Mathz.NextPowerOfTwo((int)MathF.Ceiling(width * FarawayPixelsPerTrile)),
            Mathz.NextPowerOfTwo((int)MathF.Ceiling(height * FarawayPixelsPerTrile))
        );
    }

    private string GetExportDescription(ExportKind kind)
    {
        switch (kind)
        {
            case ExportKind.FarawayThumb:
                var (width, height) = ComputeFarawayExportSize();
                return $"{width}x{height}, {FarawayPixelsPerTrile}px/trile";

            case ExportKind.MapScreen:
                var size = GetMapPixelsPerTrile();
                return $"{MapScreenSize}x{MapScreenSize}, {size}px/trile";

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private int GetMapPixelsPerTrile()
    {
        if (_cameraActor.FindComponent<MapZoomControl>() is { } zoom)
        {
            return zoom.PixelsPerTrile;
        }

        var (_, viewportHeight) = _scene.Viewport.GetSize();
        if (viewportHeight <= 0 || _camera.Size <= 0f)
        {
            return DefaultMapPixelsPerTrile;
        }

        var current = viewportHeight / _camera.Size;
        if (current <= 0f || float.IsNaN(current) || float.IsInfinity(current))
        {
            return DefaultMapPixelsPerTrile;
        }

        var exponent = (int)MathF.Round(MathF.Log2(current));
        return Math.Max(1, 1 << Math.Clamp(exponent, 0, 12));
    }

    private void DrawFarawayThumbOverlay()
    {
        var (width, height) = ComputeFarawayExportSize();
        var worldWidth = width / (float)FarawayPixelsPerTrile;
        var worldHeight = height / (float)FarawayPixelsPerTrile;
        var center = Level.Size.ToXna() / 2f;
        var right = _camera.InverseView.Right;
        var up = _camera.InverseView.Up;
        var corners = new[]
        {
            center - (right * worldWidth * 0.5f) - (up * worldHeight * 0.5f),
            center + (right * worldWidth * 0.5f) - (up * worldHeight * 0.5f),
            center + (right * worldWidth * 0.5f) + (up * worldHeight * 0.5f),
            center - (right * worldWidth * 0.5f) + (up * worldHeight * 0.5f)
        };

        var projected = corners.Select(c => _camera.Project(c, Eddy.Frame.Position)).ToArray();
        var min = new Vector2(projected.Min(p => p.X), projected.Min(p => p.Y));
        var max = new Vector2(projected.Max(p => p.X), projected.Max(p => p.Y));

        var dl = ImGui.GetForegroundDrawList();
        dl.AddRect(min.ToNumerics(), max.ToNumerics(), 0xCC00FFFF, 0f, ImDrawFlags.None, 2f);
    }

    private void DrawMapScreenOverlay()
    {
        var center = _camera.Project(_cameraActor.Transform.Position, Eddy.Frame.Position);
        var half = new Vector2(MapScreenSize / 2f);
        var min = new Vector2(center.X, center.Y) - half;
        var max = new Vector2(center.X, center.Y) + half;

        var dl = ImGui.GetForegroundDrawList();
        dl.AddRect(min.ToNumerics(), max.ToNumerics(), 0xCC00FFFF, 0f, ImDrawFlags.None, 2f);
    }

    private enum ExportKind
    {
        FarawayThumb,
        MapScreen
    }
}