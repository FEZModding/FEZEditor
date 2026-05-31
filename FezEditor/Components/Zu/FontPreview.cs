using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Zu;

internal class FontPreview
{
    public bool IsOpen
    {
        get => _isOpen;
        set => _isOpen = value;
    }

    private readonly string _title;

    private readonly ZuEditor _zu;

    private string _previewText = "The quick brown fox jumps over the lazy dog.\n" +
                                  "ABCDEFGHIJKLMNOPQRSTUVWXYZ\n" +
                                  "abcdefghijklmnopqrstuvwxyz\n" +
                                  "0123456789 !@#$%^&*()_+-=[]{}|;':\",./<>?";

    private float _previewScale = 2.0f;

    private bool _showKerning = true;

    private bool _showGlyphBounds = true;

    private bool _isOpen;

    public FontPreview(string title, ZuEditor zu)
    {
        _title = title;
        _zu = zu;
    }

    public void DrawWindow()
    {
        if (!_isOpen)
        {
            return;
        }

        ImGui.SetNextWindowSize(new NVector2(800, 600), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin($"Font Preview##{_title}", ref _isOpen, ImGuiWindowFlags.None))
        {
            ImGui.End();
            return;
        }

        ImGui.Text("Preview Text:");
        ImGui.PushFont(_zu.CharactersFont);
        ImGui.InputTextMultiline("##PreviewText", ref _previewText, 1024, new NVector2(-1, 0));
        ImGui.PopFont();

        ImGui.Text("Scale:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200f);
        ImGui.SliderFloat("##PreviewScale", ref _previewScale, 0.5f, 8.0f, "%.1fx");

        ImGui.SameLine();
        ImGui.Checkbox("Show Glyph Bounds", ref _showGlyphBounds);
        ImGui.SameLine();
        ImGui.Checkbox("Show Kerning", ref _showKerning);

        ImGui.Separator();

        if (ImGuiX.BeginChild("##PreviewCanvas", Vector2.Zero, ImGuiChildFlags.Border))
        {
            var canvasPos = ImGui.GetCursorScreenPos();
            var canvasSize = ImGui.GetContentRegionAvail();

            if (canvasSize is { X: > 1f, Y: > 1f })
            {
                var dl = ImGui.GetWindowDrawList();
                dl.PushClipRect(canvasPos, canvasPos + canvasSize, true);
                dl.AddRectFilled(canvasPos, canvasPos + canvasSize, Color.Black.PackedValue);
                DrawPreviewText(dl, canvasPos + new NVector2(10, 10));
                dl.PopClipRect();
            }

            ImGui.EndChild();
        }

        ImGui.End();
    }

    private void DrawPreviewText(ImDrawListPtr dl, NVector2 startPos)
    {
        var pos = startPos;
        var lineHeight = _zu.Font.LineSpacing * _previewScale;

        foreach (var ch in _previewText)
        {
            switch (ch)
            {
                case '\n':
                    pos.X = startPos.X;
                    pos.Y += lineHeight;
                    continue;

                case '\r':
                    continue;
            }

            var charIndex = _zu.Font.Characters.IndexOf(ch);
            if (charIndex < 0)
            {
                if (_zu.Font.DefaultCharacter.HasValue && _zu.Font.DefaultCharacter != '\u0000')
                {
                    charIndex = _zu.Font.Characters.IndexOf(_zu.Font.DefaultCharacter.Value);
                }

                if (charIndex < 0)
                {
                    continue;
                }
            }

            if (charIndex >= _zu.Font.GlyphBounds.Count)
            {
                continue;
            }

            var glyphBounds = _zu.Font.GlyphBounds[charIndex];
            var cropping = charIndex < _zu.Font.Cropping.Count
                ? _zu.Font.Cropping[charIndex]
                : new RRectangle(0, 0, glyphBounds.Width, glyphBounds.Height);

            var kerning = charIndex < _zu.Font.KerningData.Count
                ? _zu.Font.KerningData[charIndex].ToXna()
                : new Vector3(0, glyphBounds.Width, 0);

            var charStartX = pos.X;

            var renderX = pos.X + ((kerning.X + cropping.X) * _previewScale);
            var renderY = pos.Y + (cropping.Y * _previewScale);

            var texW = (float)_zu.FontTexture.Width;
            var texH = (float)_zu.FontTexture.Height;
            var uv0 = new NVector2(glyphBounds.X / texW, glyphBounds.Y / texH);
            var uv1 = new NVector2((glyphBounds.X + glyphBounds.Width) / texW,
                (glyphBounds.Y + glyphBounds.Height) / texH);

            var renderPos = new NVector2(renderX, renderY);
            var renderSize = new NVector2(glyphBounds.Width * _previewScale, glyphBounds.Height * _previewScale);

            if (glyphBounds is { Width: > 0, Height: > 0 })
            {
                dl.AddImage(_zu.FontTexturePtr, renderPos, renderPos + renderSize, uv0, uv1);
            }

            if (_showGlyphBounds && glyphBounds is { Width: > 0, Height: > 0 })
            {
                dl.AddRect(
                    renderPos,
                    renderPos + renderSize,
                    0xFFFFFF40u,
                    0f, ImDrawFlags.None, 1f);
            }

            if (_showKerning)
            {
                var advanceWidth = kerning.Y * _previewScale;
                var boxTop = pos.Y - 2f;
                var boxBottom = pos.Y + lineHeight + 2f;
                dl.AddRect(
                    new NVector2(charStartX, boxTop),
                    new NVector2(charStartX + advanceWidth, boxBottom),
                    0x80FF8040u,
                    0f, ImDrawFlags.None, 1f);
            }

            pos.X += (kerning.Y + _zu.Font.Spacing) * _previewScale;
        }
    }
}