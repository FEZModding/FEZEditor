using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Scripting;

[Entity(Model = typeof(TrileGroup), RestrictTo = new[] { ActorType.SuckBlock })]
internal interface ISuckBlock
{
    [Description("When it's completely inside its host volume")]
    event Action<int> Sucked;

    void OnSuck(int id);

    bool get_IsSucked(int id);
}