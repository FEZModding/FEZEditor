using FezEditor.Structure;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Chris;

internal interface IContext : IDisposable
{
    string TextureExportKey { get; }

    TrixelObject Materialize();

    object GetAsset(TrixelObject obj);

    Texture2D LoadTexture();

    void UpdateTexture(Texture2D texture);

    bool DrawProperties(History history);
}