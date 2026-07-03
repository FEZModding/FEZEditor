using FEZRepacker.Core.Definitions.Game.Level.Scripting;

namespace FezEditor.Scripting;

[Entity(Model = typeof(Script))]
internal interface IScript
{
    [Description("When the script timeouts or terminates")]
    event Action<int> Complete;

    void OnComplete(int id);

    [Description("Enables or disables a script")]
    void SetEnabled(int id, bool enabled);

    [Description("Evaluates a script")]
    void Evaluate(int id);
}