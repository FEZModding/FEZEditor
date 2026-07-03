using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Scripting;

[Entity(Model = typeof(NpcInstance))]
internal interface INpc
{
    [Description("Makes the NPC say a custom text line")]
    LongRunningAction Say(int id, string line, string customSound, string customAnimation);

    [Description("CarryGeezerLetter")]
    void CarryGeezerLetter(int id);
}