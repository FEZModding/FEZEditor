using FezEditor.Structure;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Eddy;

public abstract class ThumbnailBrowserSystem<TItem> : EddySystem where TItem : notnull
{
    private const int DefaultThumbSizeIndex = 2;

    private const float TileSpacing = 10f;

    private const float TilePadding = 4f;

    private const float ToolbarControlsWidth = 210f;

    private readonly int[] _thumbSizes = [16, 32, 64, 128];

    private int _thumbSizeIndex = DefaultThumbSizeIndex;

    private float _zoomWheelAccum;

    protected string FilterText { get; private set; } = string.Empty;

    protected void DrawBrowserToolbar(string filterHint)
    {
        var available = ImGui.GetContentRegionAvail().X;
        var clearButtonWidth = string.IsNullOrEmpty(FilterText)
            ? 0f
            : ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X;
        ImGui.SetNextItemWidth(Math.Max(available - ToolbarControlsWidth - clearButtonWidth, 80f));

        var filter = FilterText;
        if (ImGui.InputTextWithHint($"##Filter{GetType().Name}", filterHint, ref filter, 255))
        {
            FilterText = filter;
        }

        if (!string.IsNullOrEmpty(FilterText))
        {
            ImGui.SameLine();
            if (ImGui.Button($"{Lucide.X}##ClearFilter{GetType().Name}"))
            {
                FilterText = string.Empty;
            }
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(Lucide.ZoomIn);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Thumbnail size (Ctrl+mouse wheel)");
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f);
        ImGui.SliderInt($"##ThumbSize{GetType().Name}", ref _thumbSizeIndex, 0, _thumbSizes.Length - 1,
            $"{_thumbSizes[_thumbSizeIndex]} px");

        ImGui.SameLine();
        if (ImGui.Button($"{Lucide.RotateCcw}##ResetZoom{GetType().Name}"))
        {
            _thumbSizeIndex = DefaultThumbSizeIndex;
            _zoomWheelAccum = 0f;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Reset thumbnail size");
        }
    }

    protected bool MatchesFilter(params string[] values)
    {
        return string.IsNullOrWhiteSpace(FilterText) ||
               values.Any(value => value.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
    }

    protected unsafe void DrawTileGrid(string childId, IReadOnlyList<TItem> items, NVector2 size = default,
        bool scrollToSelection = false)
    {
        if (!ImGui.BeginChild(childId, size, ImGuiChildFlags.Borders))
        {
            ImGui.EndChild();
            return;
        }

        var availableWidth = ImGui.GetContentRegionAvail().X;
        var layout = CalculateLayout(availableWidth, items.Count, _thumbSizes[_thumbSizeIndex]);
        var contentOrigin = ImGui.GetCursorScreenPos();
        var startPosition = contentOrigin + new NVector2(layout.OuterPadding, layout.OuterPadding);
        var drawList = ImGui.GetWindowDrawList();
        var shouldScroll = scrollToSelection && ImGui.IsWindowAppearing();

        var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
        clipper.Begin(layout.RowCount, layout.StepY);
        while (clipper.Step())
        {
            for (var row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
            {
                var first = row * layout.ColumnCount;
                var last = Math.Min(first + layout.ColumnCount, items.Count);
                for (var i = first; i < last; i++)
                {
                    var item = items[i];
                    var position = startPosition + new NVector2((i % layout.ColumnCount) * layout.StepX,
                        row * layout.StepY);
                    var selected = IsSelected(item);

                    ImGui.SetCursorScreenPos(position);
                    ImGui.PushID(GetStableId(item));
                    if (ImGui.Selectable("##Tile", selected, ImGuiSelectableFlags.None, layout.ItemSize))
                    {
                        Activate(item);
                    }

                    var hovered = ImGui.IsItemHovered();
                    DrawTile(drawList, position, layout, GetThumbnail(item), GetItemLabel(item), selected, hovered);

                    if (selected && shouldScroll)
                    {
                        ImGui.SetScrollHereY(0.5f);
                    }

                    ImGui.PopID();
                }
            }
        }

        clipper.End();
        clipper.Destroy();

        ImGui.SetCursorScreenPos(contentOrigin with { Y = startPosition.Y + (layout.RowCount * layout.StepY) + layout.OuterPadding });
        ImGui.Dummy(NVector2.Zero);

        HandleZoom(items.Count, availableWidth, startPosition, layout);
        ImGui.EndChild();
    }

    protected abstract Texture2D GetThumbnail(TItem item);

    protected abstract string GetItemLabel(TItem item);

    protected abstract string GetStableId(TItem item);

    protected abstract bool IsSelected(TItem item);

    protected abstract void Activate(TItem item);

    private void HandleZoom(int itemCount, float availableWidth, NVector2 startPosition, GridLayout oldLayout)
    {
        var io = ImGui.GetIO();
        if (!ImGui.IsWindowHovered() || io.MouseWheel == 0f || !io.KeyCtrl || ImGui.IsAnyItemActive())
        {
            return;
        }

        _zoomWheelAccum += io.MouseWheel;
        if (MathF.Abs(_zoomWheelAccum) < 1f)
        {
            return;
        }

        var wholeSteps = (int)_zoomWheelAccum;
        _zoomWheelAccum -= wholeSteps;

        var mouse = io.MousePos;
        var itemX = (mouse.X - startPosition.X + (oldLayout.Spacing * 0.5f)) / oldLayout.StepX;
        var itemY = (mouse.Y - startPosition.Y + (oldLayout.Spacing * 0.5f)) / oldLayout.StepY;
        var hoveredIndex = ((int)itemY * oldLayout.ColumnCount) + (int)itemX;

        _thumbSizeIndex = Math.Clamp(_thumbSizeIndex + wholeSteps, 0, _thumbSizes.Length - 1);
        if (hoveredIndex < 0 || hoveredIndex >= itemCount)
        {
            return;
        }

        var newLayout = CalculateLayout(availableWidth, itemCount, _thumbSizes[_thumbSizeIndex]);
        var rowFraction = itemY - MathF.Floor(itemY);
        var newRelativeY = ((hoveredIndex / (float)newLayout.ColumnCount) + rowFraction) * newLayout.StepY;
        var mouseLocalY = mouse.Y - ImGui.GetWindowPos().Y;
        ImGui.SetScrollY(newRelativeY + newLayout.OuterPadding + ImGui.GetStyle().WindowPadding.Y - mouseLocalY);
    }

    private static void DrawTile(ImDrawListPtr drawList, NVector2 position, GridLayout layout, Texture2D texture,
        string label, bool selected, bool hovered)
    {
        var imageAreaMin = position + new NVector2(TilePadding, TilePadding);
        var imageAreaSize = Math.Max(layout.ThumbSize - (TilePadding * 2), 1f);
        var imageAreaMax = imageAreaMin + new NVector2(imageAreaSize);
        drawList.AddRectFilled(imageAreaMin, imageAreaMax, ImGui.GetColorU32(ImGuiCol.FrameBg));

        var scale = Math.Min(imageAreaSize / texture.Width, imageAreaSize / texture.Height);
        var imageSize = new NVector2(texture.Width * scale, texture.Height * scale);
        var imageMin = imageAreaMin + ((new NVector2(imageAreaSize) - imageSize) * 0.5f);
        drawList.AddImage(ImGuiX.Bind(texture), imageMin, imageMin + imageSize);

        var displayLabel = TruncateLabel(label, layout.ItemSize.X - (TilePadding * 2));
        var textSize = ImGui.CalcTextSize(displayLabel);
        var textPosition = new NVector2(position.X + ((layout.ItemSize.X - textSize.X) * 0.5f),
            position.Y + layout.ThumbSize + ImGui.GetStyle().ItemInnerSpacing.Y);
        var textColor = ImGui.GetColorU32(selected ? ImGuiCol.Text : ImGuiCol.TextDisabled);
        drawList.AddText(textPosition, textColor, displayLabel);

        if (hovered && displayLabel != label)
        {
            ImGui.SetTooltip(label);
        }
    }

    private static GridLayout CalculateLayout(float availableWidth, int itemCount, float thumbSize)
    {
        var itemWidth = MathF.Floor(thumbSize);
        var itemHeight = itemWidth + ImGui.GetTextLineHeightWithSpacing();
        var columns = Math.Max((int)(availableWidth / (itemWidth + TileSpacing)), 1);
        var spacing = columns > 1
            ? MathF.Floor(availableWidth - (itemWidth * columns)) / columns
            : TileSpacing;
        spacing = Math.Max(spacing, 0f);
        var rows = (itemCount + columns - 1) / columns;
        return new GridLayout(itemWidth, spacing, columns, rows,
            new NVector2(itemWidth, itemHeight), itemWidth + spacing, itemHeight + spacing,
            MathF.Floor(spacing * 0.5f));
    }

    private static string TruncateLabel(string label, float maxWidth)
    {
        if (ImGui.CalcTextSize(label).X <= maxWidth)
        {
            return label;
        }

        const string ellipsis = "...";
        if (ImGui.CalcTextSize(ellipsis).X > maxWidth)
        {
            var prefixLength = label.Length;
            while (prefixLength > 0 && ImGui.CalcTextSize(label[..prefixLength]).X > maxWidth)
            {
                prefixLength--;
            }

            return label[..prefixLength];
        }

        var length = label.Length;
        while (length > 0 && ImGui.CalcTextSize(label[..length] + ellipsis).X > maxWidth)
        {
            length--;
        }

        return length == 0 ? ellipsis : label[..length] + ellipsis;
    }

    private readonly record struct GridLayout(
        float ThumbSize, float Spacing, int ColumnCount, int RowCount,
        NVector2 ItemSize, float StepX, float StepY, float OuterPadding);
}