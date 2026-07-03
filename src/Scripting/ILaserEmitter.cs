using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Scripting;

[Entity(Model = typeof(ArtObjectInstance), RestrictTo = new[] { ActorType.LaserEmitter })]
internal interface ILaserEmitter
{
    [Description("Starts or stops an emitter")]
    void SetEnabled(int id, bool enabled);
}