using FezEditor.Tools;

namespace FezEditor.Components.Eddy;

public abstract record PaintRotationMode
{
    public string DisplayName => this switch
    {
        Copy => "Copy",
        Random => "Random",
        Fixed f => $"{FaceExtensions.SidesOnly[f.Phi]} (Scroll)",
        _ => "Fixed"
    };

    public sealed record Fixed(byte Phi) : PaintRotationMode;

    public sealed record Random(byte LastPhi) : PaintRotationMode;

    public sealed record Copy : PaintRotationMode;
}