using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Scripting;

[Entity(Model = typeof(ArtObjectInstance), RestrictTo = new[] { ActorType.Valve, ActorType.BoltHandle })]
internal interface IValve
{
    [Description("When it's unscrewed")] event Action<int> Screwed;

    [Description("When it's screwed in")] event Action<int> Unscrewed;

    void OnScrew(int id);

    void OnUnscrew(int id);

    [Description("Enables or disables a valve's rotatability")]
    void SetEnabled(int id, bool enabled);
}