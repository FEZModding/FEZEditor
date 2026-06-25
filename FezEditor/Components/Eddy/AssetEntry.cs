namespace FezEditor.Components.Eddy;

public abstract record AssetEntry
{
    public string DisplayName => this switch
    {
        Trile t => t.Name,
        ArtObject ao => ao.Name,
        BackgroundPlane bp => bp.Name,
        NonPlayableCharacter npc => npc.Name,
        _ => throw new InvalidOperationException()
    };

    private AssetEntry() { }

    public sealed record Trile(string Name, string Path, int Id) : AssetEntry;

    public sealed record ArtObject(string Name) : AssetEntry;

    public sealed record BackgroundPlane(string Name) : AssetEntry;

    public sealed record NonPlayableCharacter(string Name) : AssetEntry;
}