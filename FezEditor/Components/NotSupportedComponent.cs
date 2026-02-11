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
        const string text = "(!) Not supported...";
        ImGuiX.SetTextCentered(text);
        ImGui.Text(text);
    }
}