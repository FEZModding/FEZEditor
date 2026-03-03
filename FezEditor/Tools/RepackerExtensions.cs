using FezEditor.Structure;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Tools;

public static class RepackerExtensions
{
    public static GraphicsDevice? Gd { private get; set; }

    public static MeshSurface ConvertToMesh(VertexInstance[] vertices, ushort[] indices)
    {
        return new MeshSurface
        {
            Vertices = vertices.Select(i => i.Position.ToXna()).ToArray(),
            Normals = vertices.Select(i => i.Normal.ToXna()).ToArray(),
            TexCoords = vertices.Select(i => i.TextureCoordinate.ToXna()).ToArray(),
            Indices = indices.Select(i => (int)i).ToArray()
        };
    }

    public static Texture2D ConvertToTexture2D(RTexture2D texture)
    {
        var tex2D = new Texture2D(Gd, texture.Width, texture.Height, false, SurfaceFormat.Color);
        var data = new byte[texture.TextureData.Length];
        Buffer.BlockCopy(texture.TextureData, 0, data, 0, texture.TextureData.Length);
        tex2D.SetData(data);
        return tex2D;
    }

    public static Texture2D ConvertToTexture2D(RAnimatedTexture texture)
    {
        var tex2D = new Texture2D(Gd, texture.AtlasWidth, texture.AtlasHeight, false, SurfaceFormat.Color);
        var data = new byte[texture.TextureData.Length];
        Buffer.BlockCopy(texture.TextureData, 0, data, 0, texture.TextureData.Length);
        tex2D.SetData(data);
        return tex2D;
    }

    public static void SetAlpha(in Texture2D texture, float alpha)
    {
        var rgba = new byte[texture.Width * texture.Height * 4];
        texture.GetData(rgba);

        for (var i = 3; i < rgba.Length; i += 4)
        {
            rgba[i] = (byte)(alpha * 255f);
        }

        texture.SetData(rgba);
    }

    public static Vector2 ToXna(this RVector2 v)
    {
        return new Vector2(v.X, v.Y);
    }

    public static Vector3 ToXna(this RVector3 v)
    {
        return new Vector3(v.X, v.Y, v.Z);
    }

    public static Vector4 ToXna(this RVector4 v)
    {
        return new Vector4(v.X, v.Y, v.Z, v.W);
    }

    public static Quaternion ToXna(this RQuaternion q)
    {
        return new Quaternion(q.X, q.Y, q.Z, q.W);
    }

    public static Color ToXna(this RColor c)
    {
        return new Color(c.R, c.G, c.B, c.A);
    }

    public static Rectangle ToXna(this RRectangle r)
    {
        return new Rectangle(r.X, r.Y, r.Width, r.Height);
    }

    public static RVector2 ToRepacker(this Vector2 v)
    {
        return new RVector2(v.X, v.Y);
    }

    public static RVector3 ToRepacker(this Vector3 v)
    {
        return new RVector3(v.X, v.Y, v.Z);
    }

    public static RVector4 ToRepacker(this Vector4 v)
    {
        return new RVector4(v.X, v.Y, v.Z, v.W);
    }

    public static RQuaternion ToRepacker(this Quaternion q)
    {
        return new RQuaternion(q.X, q.Y, q.Z, q.W);
    }

    public static RColor ToRepacker(this Color c)
    {
        return new RColor(c.R, c.G, c.B, c.A);
    }

    public static RRectangle ToRepacker(this Rectangle r)
    {
        return new RRectangle(r.X, r.Y, r.Width, r.Height);
    }
}