using FezEditor.Tools;
using ImGuiNET;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components;

[UsedImplicitly]
public class MenuBar : DrawableGameComponent
{
    private Texture2D _logoTexture = null!;

    private AboutWindow? _aboutWindow;

    public MenuBar(Game game) : base(game) { }

    protected override void LoadContent()
    {
        _logoTexture = Game.Content.Load<Texture2D>("Content/Icon");
    }

    public override void Draw(GameTime gameTime)
    {
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("Help"))
            {
                ImGuiX.Image(_logoTexture, new Vector2(16, 16));
                ImGui.SameLine();
                if (ImGui.MenuItem("About FEZEditor...") && _aboutWindow == null)
                {
                    _aboutWindow = Game.CreateComponent<AboutWindow>();
                    _aboutWindow.Disposed += (_, _) => { _aboutWindow = null; };
                }

                ImGui.EndMenu();
            }

            ImGui.EndMainMenuBar();
        }
    }
}