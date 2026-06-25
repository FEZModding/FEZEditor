using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public readonly record struct ViewportFrame(
    Vector2 Position,
    bool AllowsRaycast,
    bool AllowsSelection
);