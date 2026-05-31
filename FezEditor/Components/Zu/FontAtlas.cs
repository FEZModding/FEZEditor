using FezEditor.Structure;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Zu;

internal class FontAtlas
{
    private readonly ZuEditor _zu;

    private readonly FontPreview _preview;

    private float _baseZoom = 1.0f;

    private float _zoom = 1.0f;

    private float _zoomPercent = 100f;

    private NVector2 _pan = NVector2.Zero;

    private bool _needsFit = true;

    private int _fitFrameDelay = 2;

    public FontAtlas(FontPreview preview, ZuEditor zu)
    {
        _zu = zu;
        _preview = preview;
    }

    public void Draw()
    {
        ImGui.SeparatorText("Atlas Texture");

        DrawToolbar();
        ImGui.Separator();

        if (!ImGuiX.BeginChild("##TextureCanvas", Vector2.Zero, ImGuiChildFlags.None))
        {
            ImGui.EndChild();
            return;
        }

        var canvasPos = ImGui.GetCursorScreenPos();
        var canvasSize = ImGui.GetContentRegionAvail();

        if (canvasSize.X < 1f || canvasSize.Y < 1f)
        {
            ImGui.EndChild();
            return;
        }

        HandleAutoFit(canvasSize);
        HandleInput(canvasPos, canvasSize);
        RenderTexture(canvasPos, canvasSize);

        ImGui.EndChild();
    }

    private void DrawToolbar()
    {
        ImGui.Text($"{Lucide.ZoomIn} Zoom:");
        ImGui.SameLine();

        ImGui.SetNextItemWidth(120f);
        if (ImGui.SliderFloat("##Zoom", ref _zoomPercent, 10f, 800f, "%.0f%%"))
        {
            _zoom = _baseZoom * (_zoomPercent / 100f);
        }

        ImGui.SameLine();
        if (ImGui.Button($"{Lucide.RotateCcw} Reset Zoom"))
        {
            _needsFit = true;
            _fitFrameDelay = 0;
        }

        ImGui.SameLine();
        var previewLabel = _preview.IsOpen
            ? $"{Lucide.EyeOff} Hide Preview"
            : $"{Lucide.Eye} Show Preview";
        if (ImGui.Button(previewLabel))
        {
            _preview.IsOpen = !_preview.IsOpen;
        }

        if (_zu.Font.Texture is { Width: > 0 } tex)
        {
            var sizeText = $"Texture Size: {tex.Width}x{tex.Height}px";
            var textWidth = ImGui.CalcTextSize(sizeText).X;
            var availWidth = ImGui.GetContentRegionAvail().X;
            ImGui.SameLine(ImGui.GetCursorPosX() + availWidth - textWidth);
            ImGui.TextDisabled(sizeText);
        }
    }

    private void HandleAutoFit(NVector2 canvasSize)
    {
        if (!_needsFit)
        {
            return;
        }

        if (_fitFrameDelay > 0)
        {
            _fitFrameDelay--;
        }
        else
        {
            FitToView(canvasSize);
            _needsFit = false;
        }
    }

    private void HandleInput(NVector2 canvasPos, NVector2 canvasSize)
    {
        ImGui.InvisibleButton("##Canvas", canvasSize,
            ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);

        if (!ImGui.IsItemHovered())
        {
            return;
        }

        var io = ImGui.GetIO();

        if (ImGui.IsMouseDragging(ImGuiMouseButton.Right))
        {
            _pan += io.MouseDelta;
            ClampPan(canvasSize);
        }

        if (io.MouseWheel != 0f)
        {
            var before = _zoom;
            _zoomPercent = Math.Clamp(_zoomPercent * MathF.Pow(1.1f, io.MouseWheel), 10f, 800f);
            _zoom = _baseZoom * (_zoomPercent / 100f);
            var rel = io.MousePos - canvasPos;
            _pan = rel - ((rel - _pan) * (_zoom / before));
            ClampPan(canvasSize);
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var atlasPixel = (io.MousePos - canvasPos - _pan) / _zoom;
            _zu.SelectedIndex = _zu.HitTest(atlasPixel);
        }
    }

    private void ClampPan(NVector2 canvasSize)
    {
        var imgSize = new NVector2(_zu.FontTexture.Width, _zu.FontTexture.Height) * _zoom;
        var maxPan = canvasSize * 2f;
        var minPan = -imgSize - canvasSize;
        _pan = NVector2.Clamp(_pan, minPan, maxPan);
    }

    private void RenderTexture(NVector2 canvasPos, NVector2 canvasSize)
    {
        var font = _zu.Font;
        var dl = ImGui.GetWindowDrawList();
        var imgPos = new NVector2(MathF.Round(canvasPos.X + _pan.X), MathF.Round(canvasPos.Y + _pan.Y));
        var imgSize = new NVector2(_zu.FontTexture.Width, _zu.FontTexture.Height) * _zoom;

        dl.PushClipRect(canvasPos, canvasPos + canvasSize, true);

        DrawCheckerboard(dl, imgPos, imgSize);
        dl.AddImage(_zu.FontTexturePtr, imgPos, imgPos + imgSize, new NVector2(0, 0), new NVector2(1, 1));

        for (var i = 0; i < font.Characters.Count; i++)
        {
            DrawGlyphRect(dl, i, imgPos);
        }

        dl.PopClipRect();
    }

    private void DrawGlyphRect(ImDrawListPtr dl, int index, NVector2 imgPos)
    {
        var font = _zu.Font;
        if (index >= font.GlyphBounds.Count)
        {
            return;
        }

        var gb = font.GlyphBounds[index];
        var sel = index == _zu.SelectedIndex;

        var rMin = imgPos + (new NVector2(gb.X, gb.Y) * _zoom);
        var rMax = rMin + (new NVector2(gb.Width, gb.Height) * _zoom);

        var fill = sel ? 0x3300FFFFu : 0x22FF8800u;
        var border = sel ? 0xFF00FFFFu : 0xAAFF8800u;
        var text = sel ? 0xFF00FFFFu : 0xFFFFAA00u;

        dl.AddRectFilled(rMin, rMax, fill);
        dl.AddRect(rMin, rMax, border, 0f, ImDrawFlags.None, sel ? 2f : 1f);

        if (sel && index < font.Cropping.Count)
        {
            var crop = font.Cropping[index];

            var cropMin = rMin + (new NVector2(crop.X, crop.Y) * _zoom);
            var cropMax = cropMin + (new NVector2(crop.Width, crop.Height) * _zoom);

            dl.AddRect(cropMin, cropMax, Color.Lime.PackedValue, 0f, ImDrawFlags.None, 1.5f);

            var crossSize = 4f * _zoom;
            dl.AddLine(
                rMin with { X = rMin.X - crossSize },
                rMin with { X = rMin.X + crossSize },
                Color.White.PackedValue, 1f);
            dl.AddLine(
                rMin with { Y = rMin.Y - crossSize },
                rMin with { Y = rMin.Y + crossSize },
                Color.White.PackedValue, 1f);
        }

        if (gb.Width * _zoom > 8f && index < font.Characters.Count)
        {
            var ch = font.Characters[index];
            var lbl = char.IsControl(ch) ? "?" : ch.ToString();
            dl.AddText(rMin + new NVector2(2f, 1f), text, lbl);
        }
    }

    private static void DrawCheckerboard(ImDrawListPtr dl, NVector2 pos, NVector2 size)
    {
        const float cellSize = 16f;
        const int maxCells = 10000;

        var clipMin = dl.GetClipRectMin();
        var clipMax = dl.GetClipRectMax();
        var visibleMin = NVector2.Max(new NVector2(pos.X, pos.Y), clipMin);
        var visibleMax = NVector2.Min(pos + size, clipMax);

        if (visibleMin.X >= visibleMax.X || visibleMin.Y >= visibleMax.Y)
        {
            return;
        }

        var startCol = (int)MathF.Floor((visibleMin.X - pos.X) / cellSize);
        var endCol = (int)MathF.Ceiling((visibleMax.X - pos.X) / cellSize);
        var startRow = (int)MathF.Floor((visibleMin.Y - pos.Y) / cellSize);
        var endRow = (int)MathF.Ceiling((visibleMax.Y - pos.Y) / cellSize);
        var totalCells = (endCol - startCol) * (endRow - startRow);

        if (totalCells > maxCells)
        {
            dl.AddRectFilled(visibleMin, visibleMax, Color.Gray.PackedValue);
            return;
        }

        for (var r = startRow; r < endRow; r++)
        {
            for (var c = startCol; c < endCol; c++)
            {
                var color = (r + c) % 2 == 0 ? Color.DarkGray : Color.LightGray;
                var cellMin = pos + (new NVector2(c, r) * cellSize);
                var cellMax = NVector2.Min(cellMin + new NVector2(cellSize), pos + size);
                cellMin = NVector2.Max(cellMin, pos);

                dl.AddRectFilled(cellMin, cellMax, color.PackedValue);
            }
        }
    }

    private void FitToView(NVector2 canvasSize)
    {
        var texW = _zu.FontTexture.Width;
        var texH = _zu.FontTexture.Height;

        var scaleX = canvasSize.X / texW;
        var scaleY = canvasSize.Y / texH;

        _baseZoom = Math.Min(scaleX, scaleY) * 0.95f;
        _zoomPercent = 100f;
        _zoom = _baseZoom;
        _pan = (canvasSize - (new NVector2(texW, texH) * _zoom)) * 0.5f;
    }
}
