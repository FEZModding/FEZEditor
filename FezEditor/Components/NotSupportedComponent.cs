using FezEditor.Structure;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class NotSupportedComponent : EditorComponent
{
    public NotSupportedComponent(Game game, string title) : base(game, title)
    {
    }

    public override void Draw()
    {
        var extension = ResourceService.GetExtension(Title);
        var text = $"{Lucide.TriangleAlert} Editing or previewing is not supported for this asset: {Title}{extension}";
        ImGuiX.SetTextCentered(text);
        ImGui.Text(text);
    }
}