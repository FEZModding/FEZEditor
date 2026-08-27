using FezEditor.Structure;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

internal abstract class TextureTool : BaseTool
{
    private bool _dirty;

    protected TextureTool(IChrisEditor chris) : base(chris) { }

    protected void PaintTrixel(TrixelFace face)
    {
        PaintFace(face);
        foreach (var mirrored in Chris.SymmetryMode.GetSymmetricFaces(face, Chris.Obj))
        {
            PaintFace(mirrored);
        }
    }

    private void PaintFace(TrixelFace face)
    {
        var textureData = Chris.Obj.Texture.TextureData;
        var color = Chris.PaintColor;

        var idx = Chris.Obj.GetTrixelFaceTextureDataIndex(face);

        if (Chris.PaintMode is PaintMode.Color)
        {
            textureData[idx + 0] = color.R;
            textureData[idx + 1] = color.G;
            textureData[idx + 2] = color.B;
        }
        else if (Chris.PaintMode is PaintMode.Emission)
        {
            textureData[idx + 3] = color.A;
        }

        _dirty = true;
    }

    protected void FlushPaintChanges()
    {
        if (_dirty)
        {
            _dirty = false;
            Chris.Trixels.UpdateTextureDataFrom(Chris.Obj.Texture);
        }
    }
}