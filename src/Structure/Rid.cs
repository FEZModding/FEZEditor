namespace FezEditor.Structure;

/// <summary>
/// Opaque resource identifier. All server-managed resources are referenced by RID.
/// A default RID (value 0) is invalid/null.
/// </summary>
/// <param name="Id">Value of RID.</param>
/// <param name="Type">Type of RID (for debugging purposes).</param>
public readonly record struct Rid(uint Id, Type Type)
{
    public static readonly Rid Invalid = new(0, typeof(void));

    public bool IsValid => Id > 0 && Type != typeof(void);

    public override string ToString()
    {
        return $"RID({Id}, {Type.Name})";
    }
}