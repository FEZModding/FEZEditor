using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public interface IComponent : IDisposable
{
    bool Enabled { get; set; }

    void LoadContent(IContentManager content);

    void Update(GameTime gameTime);
}