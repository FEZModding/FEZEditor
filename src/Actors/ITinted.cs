using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public interface ITinted : IComponent
{
    Color Tint { get; set; }
}