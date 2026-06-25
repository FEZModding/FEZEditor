using FezEditor.Actors;
using FezEditor.Services;
using FezEditor.Structure;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public class ViewportSystem : EddySystem
{
    private readonly Scene _scene;

    private readonly Clock _clock;

    private readonly OrientationGizmo _orientation;

    private readonly Gizmo _gizmo;

    public ViewportSystem(Scene scene, Clock clock, OrientationGizmo orientation, Gizmo gizmo)
    {
        _scene = scene;
        _clock = clock;
        _orientation = orientation;
        _gizmo = gizmo;
    }

    public override void Draw()
    {
        var size = ImGuiX.GetContentRegionAvail();
        var w = (int)size.X;
        var h = (int)size.Y;

        if (w <= 0 || h <= 0)
        {
            return;
        }

        var texture = _scene.Viewport.GetTexture();
        if (texture == null || texture.Width != w || texture.Height != h)
        {
            _scene.Viewport.SetSize(w, h);
        }

        if (texture is not { IsDisposed: false })
        {
            return;
        }

        ImGuiX.Image(texture, size);
        const ImGuiHoveredFlags hoverFlags = ImGuiHoveredFlags.AllowWhenBlockedByActiveItem |
                                             ImGuiHoveredFlags.AllowWhenBlockedByPopup;

        var position = ImGuiX.GetItemRectMin();
        var hovered = ImGui.IsItemHovered(hoverFlags);

        Eddy.Frame = new ViewportFrame(
            position,
            size,
            hovered,
            hovered && !ImGui.IsMouseDragging(ImGuiMouseButton.Right),
            hovered && !ImGui.IsMouseDragging(ImGuiMouseButton.Left)
        );

        Input.IsViewportHovered = Eddy.Frame.AllowsSelection;
        _gizmo.ViewportPosition = Eddy.Frame.Position;

        _orientation.Draw(position + new Vector2(size.X - 8f, 8f));
        ImGuiX.DrawStats(position + new Vector2(8, 8), Rendering.GetStats());

        var topCenter = position + new Vector2(size.X / 2f, 8f);
        ImGuiX.DrawClock(topCenter, _clock);
    }
}