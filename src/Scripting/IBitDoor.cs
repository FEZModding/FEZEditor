using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Scripting;

[Entity(Model = typeof(ArtObjectInstance), RestrictTo = new[] { ActorType.EightBitDoor })]
internal interface IBitDoor
{
    [Description("When it's opened")]
    event Action<int> Open;

    void OnOpen(int id);
}