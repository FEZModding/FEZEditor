using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Scripting;

[Entity(Model = typeof(BackgroundPlane), RestrictTo = new[] { ActorType.BigWaterfall })]
internal interface IBigWaterfall
{
    LongRunningAction Open(int id);
}