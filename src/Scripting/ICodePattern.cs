using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Scripting;

[Entity(Model = typeof(ArtObjectInstance), RestrictTo = new[] { ActorType.Rumbler, ActorType.CodeMachine, ActorType.QrCode })]
internal interface ICodePattern
{
    [Description("When the right pattern is input")]
    event Action<int> Activated;

    void OnActivate(int id);
}