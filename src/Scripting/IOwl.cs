namespace FezEditor.Scripting;

[Entity(Static = true)]
internal interface IOwl
{
    [Description("Number of owls collected up to now")]
    int OwlsCollected { get; }

    event Action OwlCollected;

    event Action OwlLanded;

    void OnOwlCollected();

    void OnOwlLanded();
}