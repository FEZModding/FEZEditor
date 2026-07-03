using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Scripting;

[Entity(Model = typeof(ArtObjectInstance), RestrictTo = new[] { ActorType.SpinBlock })]
internal interface ISpinBlock
{
    [Description("Enables or disables a spinblock (which ceases or resumes its spinning)")]
    void SetEnabled(int id, bool enabled);
}