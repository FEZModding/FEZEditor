using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Scripting;

[Entity(Model = typeof(ArtObjectInstance), RestrictTo = new[] { ActorType.Timeswitch })]
internal interface ITimeswitch
{
    [Description("When the screw minimally sticks out from the base (it's been screwed out)")]
    event Action<int> ScrewedOut;

    [Description("When it stop winding back in (hits the base)")]
    event Action<int> HitBase;

    void OnScrewedOut(int id);

    void OnHitBase(int id);
}