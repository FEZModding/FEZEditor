using FezEditor.Structure;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

internal class BucketTool : TextureTool
{
    private LmbState _lmb;

    public BucketTool(Game game, IChrisEditor chris) : base(game, chris)
    {
    }

    protected override void Act()
    {
        StatusService.AddHint("LMB", "Fill");
        StatusService.AddHint("LMB + Drag", "Paint Region");

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _lmb = Chris is { IsViewportHovered: true, Hit: not null } ? LmbState.Pressed : LmbState.Idle;
        }

        if (_lmb != LmbState.Idle && ImGui.IsMouseDragging(ImGuiMouseButton.Left) && Chris.SelectedFaces.Count > 0)
        {
            _lmb = LmbState.Dragging;
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            if (_lmb == LmbState.Pressed && Chris is { IsViewportHovered: true, Hit: not null })
            {
                using (Chris.History.BeginScope("Fill Trixels"))
                {
                    FloodFillTrixels(Chris.Hit.Value);
                    Chris.SelectedFaces.Clear();
                }

                FlushPaintChanges();
            }
            else if (_lmb == LmbState.Dragging && Chris.SelectedFaces.Count > 0)
            {
                using (Chris.History.BeginScope("Paint Trixels Region"))
                {
                    foreach (var face in Chris.SelectedFaces)
                    {
                        PaintTrixel(face);
                    }

                    Chris.SelectedFaces.Clear();
                }

                FlushPaintChanges();
            }

            _lmb = LmbState.Idle;
        }
    }

    protected override bool IsToolAllowed(ChrisTool tool)
    {
        return tool == ChrisTool.Bucket;
    }

    private void FloodFillTrixels(TrixelFace originFace)
    {
        var obj = Chris.Obj;
        var clickedColor = obj.GetTrixelColor(originFace);

        if (clickedColor == Chris.PaintColor)
        {
            return;
        }

        var facesToPaint = obj.FloodFillFaces(originFace, face => obj.GetTrixelColor(face) == clickedColor);

        foreach (var face in facesToPaint)
        {
            PaintTrixel(face);
        }
    }
}