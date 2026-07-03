using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Scripting;

[Entity(Model = typeof(TrileGroup), RestrictTo = new[] { ActorType.RotatingGroup })]
internal interface IRotatingGroup
{
    void Rotate(int id, bool clockwise, int turns);

    void SetEnabled(int id, bool enabled);
}