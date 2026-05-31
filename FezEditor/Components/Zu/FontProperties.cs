using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Zu;

internal class FontProperties
{
    private readonly ZuEditor _zu;

    private int _lastSelectedIndex = -1;

    public FontProperties(ZuEditor zu)
    {
        _zu = zu;
    }

    public void Draw()
    {
        DrawFontProperties();
        ImGui.Spacing();
        DrawCharacterList();
        ImGui.Spacing();
        DrawCharacterEditor();
    }

    private void DrawFontProperties()
    {
        ImGui.SeparatorText("Font Properties");
        {
            var lineSpacing = _zu.Font.LineSpacing;
            ImGui.SetNextItemWidth(120f);
            if (ImGui.InputInt("Line Spacing", ref lineSpacing))
            {
                using (_zu.History.BeginScope("Edit Line Spacing"))
                {
                    _zu.Font.LineSpacing = Math.Max(1, lineSpacing);
                }
            }

            var spacing = _zu.Font.Spacing;
            ImGui.SetNextItemWidth(120f);
            if (ImGui.InputFloat("Spacing", ref spacing, 0.5f, 1f, "%.1f"))
            {
                using (_zu.History.BeginScope("Edit Spacing"))
                {
                    _zu.Font.Spacing = spacing;
                }
            }

            var defStr = _zu.Font.DefaultCharacter == '\u0000'
                ? string.Empty
                : _zu.Font.DefaultCharacter.ToString();

            ImGui.SetNextItemWidth(60f);
            ImGui.PushFont(_zu.CharactersFont);
            if (ImGui.InputText("Default Char", ref defStr, 2))
            {
                using (_zu.History.BeginScope("Edit Default Char"))
                {
                    _zu.Font.DefaultCharacter = defStr.Length > 0 ? defStr[0] : '\u0000';
                }
            }

            ImGui.PopFont();

            ImGui.SameLine();
            ImGui.TextDisabled($"U+{(int)_zu.Font.DefaultCharacter!:X4}");
        }
    }

    private void DrawCharacterList()
    {
        var font = _zu.Font;
        var count = font.Characters.Count;
        ImGui.SeparatorText($"Characters ({count})");

        var selectionChanged = _zu.SelectedIndex != _lastSelectedIndex;

        if (ImGuiX.BeginListBox("##CharList", new Vector2(-1f, 240f)))
        {
            for (var i = 0; i < count; i++)
            {
                var character = font.Characters[i];
                var selected = i == _zu.SelectedIndex;

                var label = !char.IsControl(character)
                    ? $"[{character}]  U+{(int)character:X4}  #{i}"
                    : $"[ctrl]  U+{(int)character:X4}  #{i}";

                ImGui.PushFont(_zu.CharactersFont);
                if (ImGui.Selectable($"{label}###{i}", selected, ImGuiSelectableFlags.SpanAllColumns))
                {
                    _zu.SelectedIndex = i;
                }

                ImGui.PopFont();

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                    if (selectionChanged)
                    {
                        ImGui.SetScrollHereY(0.5f);
                    }
                }
            }

            ImGui.EndListBox();
        }

        _lastSelectedIndex = _zu.SelectedIndex;

        if (ImGuiX.Button($"{Lucide.Plus} Add", Vector2.Zero))
        {
            _zu.AddCharacter('?');
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(_zu.SelectedIndex < 0);

        if (ImGuiX.Button($"{Lucide.Copy} Duplicate", Vector2.Zero))
        {
            _zu.DuplicateSelected();
        }

        ImGui.SameLine();

        if (ImGuiX.Button($"{Lucide.Minus} Remove", Vector2.Zero))
        {
            _zu.DeleteSelected();
        }

        ImGui.EndDisabled();
    }

    private void DrawCharacterEditor()
    {
        var font = _zu.Font;
        var selectedIndex = _zu.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= font.Characters.Count)
        {
            ImGui.TextDisabled("No character selected.");
            return;
        }

        ImGui.SeparatorText("Edit Character");

        var character = font.Characters[selectedIndex];
        var str = char.IsControl(character) ? "" : character.ToString();

        ImGui.SetNextItemWidth(50f);
        ImGui.PushFont(_zu.CharactersFont);
        if (ImGui.InputText("##Character", ref str, 2) && str.Length > 0)
        {
            _zu.SetCharacter(selectedIndex, str[0]);
        }

        ImGui.PopFont();

        ImGui.SameLine();
        ImGui.Text("Character");

        ImGui.SameLine();
        ImGui.TextDisabled($"U+{(int)character:X4}");

        ImGui.SeparatorText("Glyph Bounds  (atlas UV rect)");
        ImGui.PushID("gb");
        EditRectInList(font.GlyphBounds, selectedIndex);
        ImGui.PopID();

        ImGui.SeparatorText("Cropping  (render offset)");
        ImGui.PushID("cr");
        EditRectInList(font.Cropping, selectedIndex);
        ImGui.PopID();

        ImGui.SeparatorText("Kerning Data");
        if (selectedIndex < font.KerningData.Count)
        {
            var kerning = font.KerningData[selectedIndex].ToXna();

            var x = kerning.X;
            if (ImGui.InputFloat("Left Bear.", ref x, 1f, 1f, "%.0f"))
            {
                font.KerningData[selectedIndex] = (kerning with { X = x }).ToRepacker();
            }

            var y = kerning.Y;
            if (ImGui.InputFloat("Advance", ref y, 1f, 1f, "%.0f"))
            {
                font.KerningData[selectedIndex] = (kerning with { Y = y }).ToRepacker();
            }

            var z = kerning.Z;
            if (ImGui.InputFloat("Right Bear.", ref z, 1f, 1f, "%.0f"))
            {
                font.KerningData[selectedIndex] = (kerning with { Z = z }).ToRepacker();
            }

            DrawKerningBar(kerning);
        }
    }

    private static void EditRectInList(List<RRectangle> list, int index)
    {
        while (list.Count <= index)
        {
            list.Add(new RRectangle(0, 0, 0, 0));
        }

        var r = list[index];
        int x = r.X, y = r.Y, w = r.Width, h = r.Height;

        ImGui.SetNextItemWidth(80f);
        ImGui.InputInt("X##r", ref x);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80f);
        ImGui.InputInt("Y##r", ref y);
        ImGui.SetNextItemWidth(80f);
        ImGui.InputInt("W##r", ref w);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80f);
        ImGui.InputInt("H##r", ref h);

        list[index] = new RRectangle(x, y, w, h);
    }

    private static void DrawKerningBar(Vector3 k)
    {
        const float barH = 16f;
        const float maxBarWidth = 300f;

        var left = k.X;
        var adv = k.Y;
        var right = k.Z;

        var totalUnits = MathF.Abs(left) + adv + MathF.Abs(right);
        var scale = totalUnits > 0 ? MathF.Min(4f, maxBarWidth / totalUnits) : 4f;

        var leftPad = MathF.Max(0, -left * scale);
        var totalW = Math.Max(leftPad + MathF.Max(0, left * scale) + (adv * scale) + MathF.Max(0, right * scale), 80f);

        ImGui.Spacing();
        ImGui.TextDisabled("Preview:");

        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();

        dl.AddRectFilled(pos, pos + new NVector2(totalW, barH), Color.Black.PackedValue);

        var cx = pos.X + leftPad;

        if (left != 0f)
        {
            var col = left > 0 ? Color.DarkOrange : Color.DarkBlue;
            var x0 = left > 0 ? cx : cx + (left * scale);
            var x1 = left > 0 ? cx + (left * scale) : cx;
            dl.AddRectFilled(pos with { X = x0 }, new NVector2(x1, pos.Y + barH), col.PackedValue);
            cx += left * scale;
        }

        dl.AddRectFilled(pos with { X = cx }, new NVector2(cx + (adv * scale), pos.Y + barH),
            Color.Green.PackedValue);
        cx += adv * scale;

        if (right != 0f)
        {
            var col = right > 0 ? Color.LightBlue : Color.Orange;
            var x0 = right > 0 ? cx : cx + (right * scale);
            var x1 = right > 0 ? cx + (right * scale) : cx;
            dl.AddRectFilled(pos with { X = x0 }, new NVector2(x1, pos.Y + barH), col.PackedValue);
        }

        dl.AddRect(pos, pos + new NVector2(totalW, barH), Color.Gray.PackedValue);

        ImGui.Dummy(new NVector2(totalW, barH));
        ImGui.TextDisabled($"L:{left:F0}  Adv:{adv:F0}  R:{right:F0}");
    }
}
