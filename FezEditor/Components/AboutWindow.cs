using FezEditor.Tools;
using ImGuiNET;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components;

[UsedImplicitly]
public class AboutWindow : DrawableGameComponent
{
    private Texture2D _logoTexture = null!;

    private bool _open = true;

    public AboutWindow(Game game) : base(game)
    {
    }

    protected override void LoadContent()
    {
        _logoTexture = Game.Content.Load<Texture2D>("Content/Icon");
    }

    public override void Update(GameTime gameTime)
    {
        if (!_open)
        {
            Game.RemoveComponent(this);
        }
    }

    public override void Draw(GameTime gameTime)
    {
        ImGui.SetNextWindowCollapsed(!_open, ImGuiCond.FirstUseEver);
        ImGuiX.SetNextWindowSize(new Vector2(640, 0), ImGuiCond.FirstUseEver);

        var center = ImGui.GetMainViewport().GetCenter().ToXna();
        ImGuiX.SetNextWindowPos(center, ImGuiCond.Appearing, Vector2.One / 2);

        if (ImGui.Begin("About FEZEditor", ref _open,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoMove))
        {
            ImGuiX.BeginChild("content_pane", new Vector2(0, -30), ImGuiChildFlags.None);
            {
                ImGuiX.Image(_logoTexture, new Vector2(64, 64));
                ImGui.SameLine();
                ImGui.BeginGroup();
                ImGui.NewLine();
                ImGui.Text("FEZEDITOR");
                ImGui.Text("Developed by ");
                ImGui.SameLine(0, 0);
                ImGuiX.Hyperlink("zerocker", "https://github.com/zrckr");
                ImGui.Text("Powered by ");
                ImGui.SameLine(0, 0);
                ImGuiX.Hyperlink("FNA", "https://github.com/FNA-XNA/FNA");
                ImGui.SameLine(0, 0);
                ImGui.Text(" and ");
                ImGui.SameLine(0, 0);
                ImGuiX.Hyperlink("FEZRepacker", "https://github.com/FEZModding/FEZRepacker");
                ImGui.EndGroup();
            }
            ImGui.EndChild();

            ImGui.SetCursorPosX((ImGui.GetWindowWidth() - 80) / 2);
            if (ImGuiX.Button("OK", new Vector2(80, 0)))
            {
                _open = false;
            }
        }

        ImGui.End();
    }
}