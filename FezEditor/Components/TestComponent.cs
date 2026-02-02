using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class TestComponent : DrawableGameComponent
{
    public TestComponent(Game game) : base(game)
    {
        Enabled = true;
        DrawOrder = 10001;
    }

    public override void Draw(GameTime gameTime)
    {
        ImGui.ShowAboutWindow();
        ImGui.ShowDemoWindow();
    }
}