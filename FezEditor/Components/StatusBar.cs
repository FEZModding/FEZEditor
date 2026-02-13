using FezEditor.Services;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class StatusBar : DrawableGameComponent
{
    private readonly EditorService _editorService;

    public StatusBar(Game game) : base(game)
    {
        _editorService = game.GetService<EditorService>();
    }

    public void Draw()
    {
        // TODO: implement this
    }
}
