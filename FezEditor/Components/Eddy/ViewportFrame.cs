using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public readonly record struct ViewportFrame(
    Vector2 Position,
    Vector2 Size,
    bool IsHovered,
    bool AllowsRaycast,
    bool AllowsSelection
);