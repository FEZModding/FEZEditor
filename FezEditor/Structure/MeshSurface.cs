using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using Microsoft.Xna.Framework;

namespace FezEditor.Structure;

public class MeshSurface
{
    public required Vector3[] Vertices;
    public required int[] Indices;
    public Vector3[]? Normals;
    public Color[]? Colors;
    public Vector2[]? TexCoords;

    public static MeshSurface CreateTestTriangle()
    {
        return new MeshSurface
        {
            Vertices = new[]
            {
                new Vector3(0.0f, 0.5f, 0f), // top
                new Vector3(0.5f, -0.5f, 0f), // bottom-right
                new Vector3(-0.5f, -0.5f, 0f) // bottom-left
            },
            Indices = new[] { 0, 1, 2 },
            Colors = new[]
            {
                Color.Red,
                Color.Green,
                Color.Blue
            }
        };
    }

    public static MeshSurface CreateBox(Vector3 size)
    {
        size /= 2f;
        return new MeshSurface
        {
            Vertices = new[]
            {
                new Vector3(-1f, -1f, -1f) * size,
                new Vector3(-1f, 1f, -1f) * size,
                new Vector3(1f, 1f, -1f) * size,
                new Vector3(1f, -1f, -1f) * size,
                new Vector3(1f, -1f, -1f) * size,
                new Vector3(1f, 1f, -1f) * size,
                new Vector3(1f, 1f, 1f) * size,
                new Vector3(1f, -1f, 1f) * size,
                new Vector3(1f, -1f, 1f) * size,
                new Vector3(1f, 1f, 1f) * size,
                new Vector3(-1f, 1f, 1f) * size,
                new Vector3(-1f, -1f, 1f) * size,
                new Vector3(-1f, -1f, 1f) * size,
                new Vector3(-1f, 1f, 1f) * size,
                new Vector3(-1f, 1f, -1f) * size,
                new Vector3(-1f, -1f, -1f) * size,
                new Vector3(-1f, -1f, -1f) * size,
                new Vector3(-1f, -1f, 1f) * size,
                new Vector3(1f, -1f, 1f) * size,
                new Vector3(1f, -1f, -1f) * size,
                new Vector3(-1f, 1f, -1f) * size,
                new Vector3(-1f, 1f, 1f) * size,
                new Vector3(1f, 1f, 1f) * size,
                new Vector3(1f, 1f, -1f) * size
            },
            Normals = new[]
            {
                -Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitZ,
                Vector3.UnitX, Vector3.UnitX, Vector3.UnitX, Vector3.UnitX,
                Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ,
                -Vector3.UnitX, -Vector3.UnitX, -Vector3.UnitX, -Vector3.UnitX,
                -Vector3.UnitY, -Vector3.UnitY, -Vector3.UnitY, -Vector3.UnitY,
                Vector3.UnitY, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY
            },
            Indices = new[]
            {
                0, 2, 1, 0, 3, 2, 4, 6, 5,
                4, 7, 6, 8, 10, 9, 8, 11, 10,
                12, 14, 13, 12, 15, 14, 16, 17, 18,
                16, 18, 19, 20, 22, 21, 20, 23, 22
            }
        };
    }

    public static MeshSurface CreateQuad(Vector3 size, bool doubleSided = false)
    {
        size /= 2f;
        return new MeshSurface
        {
            Vertices = new[]
            {
                new Vector3(-size.X, -size.Y, 0),
                new Vector3(size.X, -size.Y, 0),
                new Vector3(-size.X, size.Y, 0),
                new Vector3(size.X, size.Y, 0)
            },
            Normals = new[]
            {
                Vector3.Forward,
                Vector3.Forward,
                Vector3.Forward,
                Vector3.Forward
            },
            TexCoords = new[]
            {
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(0, 0),
                new Vector2(1, 0)
            },
            Indices = doubleSided
                ? new[]
                {
                    // Front face triangles (counter-clockwise when viewed from front)
                    0, 1, 2, // Triangle 1
                    2, 1, 3, // Triangle 2

                    // Back face triangles (clockwise when viewed from front)
                    0, 2, 1, // Triangle 1 (reversed order)
                    2, 3, 1 // Triangle 2 (reversed order)
                }
                : new[]
                {
                    0, 1, 2,
                    2, 1, 3
                }
        };
    }

    public static MeshSurface CreateFaceQuad(Vector3 size, FaceOrientation face)
    {
        var normal = face.AsVector();
        var right = face.RightVector();
        var up = face.UpVector();
        var center = normal * size / 2f;

        // Half-extents along each axis
        var hr = right * size / 2f;
        var hu = up * size / 2f;

        return new MeshSurface
        {
            Vertices = new[]
            {
                center - hr - hu, // bottom-left
                center + hr - hu, // bottom-right
                center - hr + hu, // top-left
                center + hr + hu // top-right
            },
            Normals = new[] { normal, normal, normal, normal },
            TexCoords = new[]
            {
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(0, 0),
                new Vector2(1, 0)
            },
            Indices = new[]
            {
                0, 1, 2,
                2, 1, 3
            }
        };
    }

    public static MeshSurface CreateWireframeBox(Vector3 size, Color color)
    {
        size /= 2f;
        var corners = new[]
        {
            new Vector3(-size.X, -size.Y, -size.Z),
            new Vector3(size.X, -size.Y, -size.Z),
            new Vector3(size.X, size.Y, -size.Z),
            new Vector3(-size.X, size.Y, -size.Z),
            new Vector3(-size.X, -size.Y, size.Z),
            new Vector3(size.X, -size.Y, size.Z),
            new Vector3(size.X, size.Y, size.Z),
            new Vector3(-size.X, size.Y, size.Z)
        };

        return new MeshSurface
        {
            Vertices = corners,
            Colors = Enumerable.Repeat(color, corners.Length).ToArray(),
            Indices = new[]
            {
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                0, 4, 1, 5, 2, 6, 3, 7
            }
        };
    }
}