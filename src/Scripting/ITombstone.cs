namespace FezEditor.Scripting;

[Entity(Static = true)]
internal interface ITombstone
{
    [Description("When more than one tombstones are aligned")]
    event Action MoreThanOneAligned;

    void OnMoreThanOneAligned();

    int get_AlignedCount();

    void UpdateAlignCount(int count);
}