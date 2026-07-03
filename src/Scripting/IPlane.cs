using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Scripting;

[Entity(Model = typeof(BackgroundPlane))]
internal interface IPlane
{
    LongRunningAction FadeIn(int id, float seconds);

    LongRunningAction FadeOut(int id, float seconds);

    LongRunningAction Flicker(int id, float factor);
}