using System.Runtime.CompilerServices;
using FezEditor.Structure;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using Microsoft.Xna.Framework;

namespace FezEditor.Tools;

public static class Mathz
{
    public static readonly Vector3 XzMask = Vector3.UnitX + Vector3.UnitZ;

    public static readonly Color TransparentBlack = Color.Black with { A = 0 };

    public const float TrixelSize = 1f / 16f;

    private const float Deg2Rad = MathF.PI / 180f;

    private const float Rad2Deg = 180f / MathF.PI;

    public static bool IsEqualApprox(float lhs, float rhs)
    {
        return Math.Abs(lhs - rhs) < float.Epsilon;
    }

    public static bool IsZeroApprox(float value)
    {
        return MathF.Abs(value) < float.Epsilon;
    }

    public static float Frac(float value)
    {
        return value - (int)value;
    }

    public static Matrix CreateTextureTransform(Rectangle rectangle, Vector2 size)
    {
        return new Matrix(
            rectangle.Width / size.X, 0f, 0f, 0f,
            0f, rectangle.Height / size.Y, 0f, 0f,
            rectangle.X / size.X,
            rectangle.Y / size.Y, 1f, 0f,
            0f, 0f, 0f, 0f
        );
    }

    public static Quaternion CreateYBillboard(Matrix view, Vector3 position)
    {
        var cameraPos = Matrix.Invert(view).Translation;
        var toCamera = cameraPos - position;
        var angleY = (float)Math.Atan2(toCamera.X, toCamera.Z);
        return Quaternion.CreateFromAxisAngle(Vector3.Up, angleY);
    }

    public static BoundingBox ComputeBoundingBox(Vector3 position, Quaternion rotation, Vector3 scale, Vector3 size)
    {
        var halfExtents = size * 0.5f;
        var worldMatrix = Matrix.CreateScale(scale) *
                          Matrix.CreateFromQuaternion(rotation) *
                          Matrix.CreateTranslation(position);

        var localCorners = new Vector3[]
        {
            new(-halfExtents.X, -halfExtents.Y, -halfExtents.Z), // left-bottom-back
            new(halfExtents.X, -halfExtents.Y, -halfExtents.Z), // right-bottom-back
            new(-halfExtents.X, halfExtents.Y, -halfExtents.Z), // left-top-back
            new(halfExtents.X, halfExtents.Y, -halfExtents.Z), // right-top-back
            new(-halfExtents.X, -halfExtents.Y, halfExtents.Z), // left-bottom-front
            new(halfExtents.X, -halfExtents.Y, halfExtents.Z), // right-bottom-front
            new(-halfExtents.X, halfExtents.Y, halfExtents.Z), // left-top-front
            new(halfExtents.X, halfExtents.Y, halfExtents.Z) // right-top-front
        };

        var worldCorners = new Vector3[8];
        for (var i = 0; i < 8; i++)
        {
            worldCorners[i] = Vector3.Transform(localCorners[i], worldMatrix);
        }

        var min = worldCorners[0];
        var max = worldCorners[0];
        for (var i = 1; i < 8; i++)
        {
            min = Vector3.Min(min, worldCorners[i]);
            max = Vector3.Max(max, worldCorners[i]);
        }

        return new BoundingBox(min, max);
    }

    public static FaceOrientation DetermineFace(BoundingBox box, Ray ray, float distance)
    {
        var point = ray.Position + (ray.Direction * distance);
        var center = (box.Min + box.Max) / 2f;
        var bounds = (box.Max - box.Min) / 2f;

        var local = point - center;
        var abs = new Vector3
        {
            X = MathF.Abs(local.X / bounds.X),
            Y = MathF.Abs(local.Y / bounds.Y),
            Z = MathF.Abs(local.Z / bounds.Z)
        };

        Vector3 normal;
        if (abs.X > abs.Y && abs.X > abs.Z)
        {
            normal = new Vector3(MathF.Sign(local.X), 0, 0);
        }
        else if (abs.Y > abs.Z)
        {
            normal = new Vector3(0, MathF.Sign(local.Y), 0);
        }
        else
        {
            normal = new Vector3(0, 0, MathF.Sign(local.Z));
        }

        return FaceExtensions.OrientationFromDirection(normal);
    }

    public static Vector3 Abs(this Vector3 vector)
    {
        return new Vector3(
            Math.Abs(vector.X),
            Math.Abs(vector.Y),
            Math.Abs(vector.Z)
        );
    }

    public static Vector3 Round(this Vector3 vector, int decimals = 10)
    {
        return new Vector3(
            MathF.Round(vector.X, decimals),
            MathF.Round(vector.Y, decimals),
            MathF.Round(vector.Z, decimals)
        );
    }

    public static float Between(this Random random, float min, float max)
    {
        return min + ((float)random.NextDouble() * (max - min));
    }

    // Decomposes to yaw/pitch/roll matching CreateFromYawPitchRoll (intrinsic Y-X-Z).
    public static Vector3 ToYawPitchRollDegrees(this Quaternion q)
    {
        // Yaw (Y-axis)
        var sinyCosp = 2 * ((q.W * q.Y) + (q.Z * q.X));
        var cosyCosp = 1 - (2 * ((q.Y * q.Y) + (q.X * q.X)));
        var yaw = MathF.Atan2(sinyCosp, cosyCosp);

        // Pitch (X-axis)
        var sinp = 2 * ((q.W * q.X) - (q.Y * q.Z));
        var pitch = MathF.Abs(sinp) >= 1
            ? MathF.CopySign(MathF.PI / 2, sinp)
            : MathF.Asin(sinp);

        // Roll (Z-axis)
        var sinrCosp = 2 * ((q.W * q.Z) + (q.Y * q.X));
        var cosrCosp = 1 - (2 * ((q.Z * q.Z) + (q.X * q.X)));
        var roll = MathF.Atan2(sinrCosp, cosrCosp);

        return new Vector3(yaw, pitch, roll) * Rad2Deg;
    }

    public static Quaternion FromYawPitchRollDegrees(Vector3 degrees)
    {
        var yawPitchRoll = degrees * Deg2Rad;
        var quaternion = Quaternion.CreateFromYawPitchRoll(yawPitchRoll.X, yawPitchRoll.Y, yawPitchRoll.Z);
        quaternion.Normalize();
        return quaternion;
    }

    public static Quaternion LookRotation(Vector3 forward, Vector3? up = null)
    {
        up ??= Vector3.UnitY;

        var dot = Vector3.Dot(up.Value, forward);
        if (dot > 0.9999f)
        {
            return Quaternion.Identity;
        }

        if (dot < -0.9999f)
        {
            return Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI);
        }

        var cross = Vector3.Cross(up.Value, forward);
        return Quaternion.CreateFromAxisAngle(Vector3.Normalize(cross), MathF.Acos(dot));
    }

    public static float? IntersectsTriangle(this Ray ray, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        var edge1 = v1 - v0;
        var edge2 = v2 - v0;
        var h = Vector3.Cross(ray.Direction, edge2);
        var a = Vector3.Dot(edge1, h);
        if (MathF.Abs(a) < float.Epsilon)
        {
            return null;
        }

        var f = 1f / a;
        var s = ray.Position - v0;
        var u = f * Vector3.Dot(s, h);
        if (u is < 0f or > 1f)
        {
            return null;
        }

        var q = Vector3.Cross(s, edge1);
        var v = f * Vector3.Dot(ray.Direction, q);
        if (v < 0f || u + v > 1f)
        {
            return null;
        }

        var t = f * Vector3.Dot(edge2, q);
        return t > float.Epsilon ? t : null;
    }

    public static int FezRound(double value)
    {
        if (value < 0.0)
        {
            return (int)(value - 0.5);
        }

        return (int)(value + 0.5);
    }

    public static Vector3 ClampWithinEmplacement(this Vector3 position, TrileEmplacement emplacement)
    {
        var boundingBox = GetEmplacementPositionBounds(emplacement);
        return Vector3.Clamp(position, boundingBox.Min, boundingBox.Max);
    }

    public static BoundingBox GetEmplacementPositionBounds(TrileEmplacement emplacement)
    {
        var min = new Vector3(
            GetEmplacementPositionMin(emplacement.X),
            GetEmplacementPositionMin(emplacement.Y),
            GetEmplacementPositionMin(emplacement.Z));

        var max = new Vector3(
            GetEmplacementPositionMax(emplacement.X),
            GetEmplacementPositionMax(emplacement.Y),
            GetEmplacementPositionMax(emplacement.Z));

        return new BoundingBox(min, max);
    }

    private static float GetEmplacementPositionMin(int emplacement)
    {
        var min = emplacement - 0.5f;
        if (emplacement <= 0)
        {
            min = MathF.BitIncrement(min);
        }

        return min;
    }

    private static float GetEmplacementPositionMax(int emplacement)
    {
        var max = emplacement + 0.5f;
        if (emplacement >= 0)
        {
            max = MathF.BitDecrement(max);
        }

        return max;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TrileEmplacement Add(this TrileEmplacement a, TrileEmplacement b)
    {
        return new TrileEmplacement(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TrileEmplacement Sub(this TrileEmplacement a, TrileEmplacement b)
    {
        return new TrileEmplacement(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RVector3 AsVector(this TrileEmplacement a)
    {
        return new RVector3(a.X, a.Y, a.Z);
    }
}