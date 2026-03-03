using Microsoft.Xna.Framework;

namespace FezEditor.Structure;

public enum TrileRotation : byte
{
    Back,
    Left,
    Front,
    Right
}

public static class TrileRotationExtensions
{
    private static readonly float[] Angles = new[]
    {
        -MathF.Tau / 2f,
        -MathF.Tau / 4f,
        +MathF.Tau * 0f,
        +MathF.Tau / 4f
    };

    public static Quaternion AsQuaternion(this TrileRotation rot)
    {
        return Quaternion.CreateFromAxisAngle(Vector3.Up, Angles[(int)rot]);
    }

    public static float AsPhi(this TrileRotation rot)
    {
        return Angles[(int)rot];
    }
}