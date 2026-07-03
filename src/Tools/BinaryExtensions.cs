using Microsoft.Xna.Framework;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Tools;

public static class BinaryExtensions
{
    #region Reading

    public static string? ReadNullableString(this BinaryReader reader)
    {
        return reader.ReadBoolean()
            ? reader.ReadString()
            : null;
    }

    public static float? ReadNullableSingle(this BinaryReader reader)
    {
        return reader.ReadBoolean()
            ? reader.ReadSingle()
            : null;
    }

    public static Vector3 ReadVector3(this BinaryReader reader)
    {
        return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }

    public static DateTime ReadDateTime(this BinaryReader reader)
    {
        return DateTime.FromFileTimeUtc(reader.ReadInt64());
    }

    public static TimeSpan ReadTimeSpan(this BinaryReader reader)
    {
        return new TimeSpan(reader.ReadInt64());
    }

    public static TrileEmplacement ReadTrileEmplacement(this BinaryReader reader)
    {
        return new TrileEmplacement(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
    }

    #endregion

    #region Writing

    public static void WriteNullable(this BinaryWriter writer, string? @string)
    {
        var nonEmpty = !string.IsNullOrEmpty(@string);
        writer.Write(nonEmpty);
        if (nonEmpty)
        {
            writer.Write(@string ?? "");
        }
    }

    public static void WriteNullable(this BinaryWriter writer, float? single)
    {
        writer.Write(single.HasValue);
        if (single.HasValue)
        {
            writer.Write(single.Value);
        }
    }

    public static void Write(this BinaryWriter writer, Vector3 vector)
    {
        writer.Write(vector.X);
        writer.Write(vector.Y);
        writer.Write(vector.Z);
    }

    public static void Write(this BinaryWriter writer, DateTime dateTime)
    {
        writer.Write(dateTime.Ticks);
    }

    public static void Write(this BinaryWriter writer, TimeSpan timeSpan)
    {
        writer.Write(timeSpan.Ticks);
    }

    public static void Write(this BinaryWriter writer, TrileEmplacement emplacement)
    {
        writer.Write(emplacement.X);
        writer.Write(emplacement.Y);
        writer.Write(emplacement.Z);
    }

    #endregion
}