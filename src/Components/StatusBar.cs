using FezEditor.Services;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class StatusBar : DrawableGameComponent
{
    private readonly StatusService _statusService;

    public StatusBar(Game game) : base(game)
    {
        _statusService = game.GetService<StatusService>();
    }

    public void Draw()
    {
        var (left, right, activity) = _statusService.GetSnapshot();
        var hintText = string.Join(" | ", left.Select(FormatStatusHint));
        var statusText = activity == null || string.IsNullOrEmpty(hintText)
            ? activity?.Text ?? hintText
            : $"{activity.Text} | {hintText}";

        ImGui.Separator();
        if (ImGuiX.BeginChild("StatusBar", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar))
        {
            const float rightItemSpacing = 16f;
            var rightItems = BuildRightStatusItems(right, rightItemSpacing);
            var rightContentX = rightItems.Count > 0
                ? rightItems.Min(item => item.X)
                : ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X;
            var progressWidth = activity?.Progress != null ? 160f : 0f;
            var progressSpacing = progressWidth > 0 ? 8f : 0f;
            var textWidth = rightContentX - ImGui.GetCursorPosX() - progressWidth - progressSpacing - 16f;
            var visibleStatus = Ellipsize(statusText, textWidth);
            var drewLeftContent = false;

            if (!string.IsNullOrEmpty(visibleStatus))
            {
                ImGui.TextUnformatted(visibleStatus);
                drewLeftContent = true;
            }

            if (activity?.Progress is { } progress)
            {
                if (drewLeftContent)
                {
                    ImGui.SameLine(0, progressSpacing);
                }

                ImGuiX.ProgressBar(progress, new Vector2(progressWidth, 0), $"{progress * 100:F0}%");
                drewLeftContent = true;
            }

            if (drewLeftContent)
            {
                ImGui.SameLine();
            }

            DrawRightStatusItems(rightItems);
        }

        ImGui.EndChild();
    }

    private static List<RightStatusItem> BuildRightStatusItems(
        IReadOnlyList<StatusHint> hints,
        float spacing)
    {
        var items = new List<RightStatusItem>();
        var maxItemWidth = Math.Min(480f, ImGui.GetWindowWidth() * 0.35f);
        var cursorX = ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X;

        for (var i = hints.Count - 1; i >= 0; i--)
        {
            var text = FormatStatusHint(hints[i]);
            if (items.Count > 0)
            {
                text += " |";
            }

            var visible = Ellipsize(text, maxItemWidth);
            if (string.IsNullOrEmpty(visible))
            {
                continue;
            }

            var width = ImGui.CalcTextSize(visible).X;
            cursorX -= width;
            items.Add(new RightStatusItem(cursorX, visible, text));
            cursorX -= spacing;
        }

        return items;
    }

    private static void DrawRightStatusItems(List<RightStatusItem> items)
    {
        var cursorY = ImGui.GetCursorPosY();
        foreach (var item in items)
        {
            ImGui.SetCursorPos(new NVector2(item.X, cursorY));
            ImGui.TextDisabled(item.VisibleText);
            if (ImGui.IsItemHovered() && item.VisibleText != item.FullText)
            {
                ImGui.SetTooltip(item.FullText);
            }
        }
    }

    private static string FormatStatusHint(StatusHint hint)
    {
        return string.IsNullOrEmpty(hint.Binding)
            ? hint.Label
            : $"{hint.Binding} - {hint.Label}";
    }

    private static string Ellipsize(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0f)
        {
            return string.Empty;
        }

        if (ImGui.CalcTextSize(text).X <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        if (ImGui.CalcTextSize(ellipsis).X > maxWidth)
        {
            return string.Empty;
        }

        var low = 0;
        var high = text.Length;
        while (low < high)
        {
            var length = (low + high + 1) / 2;
            if (ImGui.CalcTextSize(text[..length] + ellipsis).X <= maxWidth)
            {
                low = length;
            }
            else
            {
                high = length - 1;
            }
        }

        return text[..low].TrimEnd() + ellipsis;
    }

    private readonly record struct RightStatusItem(float X, string VisibleText, string FullText);
}