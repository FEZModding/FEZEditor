namespace FezEditor.Structure;

public abstract record ResourceEntry(string Path)
{
    public sealed record File(string Path, string Extension) : ResourceEntry(Path);

    public sealed record Directory(string Path) : ResourceEntry(Path);
}