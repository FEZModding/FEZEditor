namespace FezEditor.Structure;

[Flags]
public enum EditorFlags : ulong
{
    None = 0,
    SaveFile = 1,
    CloseFile = 1 << 1,
    Undo = 1 << 2,
    Redo = 1 << 3,
    QuitToWelcome = 1 << 4
}