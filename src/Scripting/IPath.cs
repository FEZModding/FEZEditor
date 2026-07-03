using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Scripting;

[Entity(Model = typeof(MovementPath))]
internal interface IPath
{
    [Description("Applies the whole path to the camera")]
    LongRunningAction Start(int id, bool inTransition, bool outTransition);
}