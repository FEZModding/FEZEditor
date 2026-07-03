using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Scripting;

[Entity(Model = typeof(ArtObjectInstance), RestrictTo = new[] { ActorType.LaserReceiver })]
internal interface ILaserReceiver
{
    [Description("When a receiver receives a laser")]
    event Action<int> Activate;

    void OnActivated(int id);
}