using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Scripting;

[Entity(Model = typeof(TrileGroup), RestrictTo = new[] { ActorType.PushSwitch, ActorType.ExploSwitch, ActorType.PushSwitchPermanent })]
internal interface ISwitch
{
    [Description("When a bomb explodes near this switch")]
    event Action<int> Explode;

    [Description("When this switch is pushed completely")]
    [EndTrigger("Lift")]
    event Action<int> Push;

    [Description("When this switch is lifted back up")]
    event Action<int> Lift;

    void OnExplode(int id);

    void OnPush(int id);

    void OnLift(int id);

    [Description("Activates this switch")]
    void Activate(int id);

    [Description("Changes the visual of this switch's triles")]
    LongRunningAction ChangeTrile(int id, int newTrileId);
}