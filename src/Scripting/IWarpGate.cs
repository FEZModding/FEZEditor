using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Scripting;

[Entity(Model = typeof(ArtObjectInstance), RestrictTo = new[] { ActorType.WarpGate })]
internal interface IWarpGate
{
    void SetEnabled(int id, bool enabled);
}