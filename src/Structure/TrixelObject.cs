using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using Microsoft.Xna.Framework;

namespace FezEditor.Structure;

public class TrixelObject
{
    public Vector3 Size
    {
        get => _size;
        set
        {
            if (_size != value)
            {
                _size = value;
                var needed = ((Width * Height * Depth) + 7) / 8;
                if (MissingTrixels.Length != needed)
                {
                    MissingTrixels = new byte[needed];
                }

                _visibleFaces = _visibleFaces.Marked();
            }
        }
    }

    [JsonConverter(typeof(Base64Converter))]
    public byte[] MissingTrixels
    {
        get;
        set
        {
            field = value;
            _visibleFaces = _visibleFaces.Marked();
        }
    } = Array.Empty<byte>();

    [JsonConverter(typeof(CompressConverter))]
    public RTexture2D Texture { get; set; } = new();

    [JsonIgnore] public Vector3 Offset { get; set; } = Vector3.Zero;

    public TrixelProperties? Properties { get; set; }

    public int Width => (int)(Size.X / Mathz.TrixelSize);

    public int Height => (int)(Size.Y / Mathz.TrixelSize);

    public int Depth => (int)(Size.Z / Mathz.TrixelSize);

    public IReadOnlyList<TrixelFace> VisibleFaces
    {
        get
        {
            if (_visibleFaces.IsDirty)
            {
                _visibleFaces = new Dirty<TrixelFace[]>(RebuildVisualFaces().ToArray());
            }

            return _visibleFaces.Value;
        }
    }

    private Vector3 _size;

    private Dirty<TrixelFace[]> _visibleFaces = new(Array.Empty<TrixelFace>());

    public void CopyFrom(TrixelObject other)
    {
        _size = other._size;
        MissingTrixels = other.MissingTrixels;
        Texture = other.Texture;
        Properties = other.Properties;
        _visibleFaces = _visibleFaces.Marked();
    }

    public void Resize(Vector3 newSize)
    {
        if (Size != newSize)
        {
            var oldW = Width;
            var oldH = Height;
            var oldD = Depth;
            Size = newSize;
            ReallocateBitset(oldW, oldH, oldD);
        }
    }

    public bool SizeContains(Vector3I emplacement)
    {
        return emplacement.X >= 0 && emplacement.X < Width &&
               emplacement.Y >= 0 && emplacement.Y < Height &&
               emplacement.Z >= 0 && emplacement.Z < Depth;
    }

    public bool IsMissing(Vector3I emplacement)
    {
        return IsMissing(BitIndex(emplacement));
    }

    private bool IsMissing(int bitIndex)
    {
        return (MissingTrixels[bitIndex >> 3] & (1 << (bitIndex & 7))) != 0;
    }

    public void SetMissing(Vector3I emplacement, bool missing)
    {
        var i = BitIndex(emplacement);
        if (missing)
        {
            MissingTrixels[i >> 3] |= (byte)(1 << (i & 7));
        }
        else
        {
            MissingTrixels[i >> 3] &= (byte)~(1 << (i & 7));
        }

        _visibleFaces = _visibleFaces.Marked();
    }

    private void ReallocateBitset(int oldW, int oldH, int oldD)
    {
        var w = Width;
        var h = Height;
        var d = Depth;
        var needed = ((w * h * d) + 7) / 8;

        if (MissingTrixels.Length == needed)
        {
            return;
        }

        var oldBytes = MissingTrixels;
        MissingTrixels = new byte[needed];

        var copyW = Math.Min(w, oldW);
        var copyH = Math.Min(h, oldH);
        var copyD = Math.Min(d, oldD);

        for (var x = 0; x < copyW; x++)
        {
            for (var y = 0; y < copyH; y++)
            {
                for (var z = 0; z < copyD; z++)
                {
                    var oldI = x + (y * oldW) + (z * oldW * oldH);
                    if ((oldBytes[oldI >> 3] & (1 << (oldI & 7))) != 0)
                    {
                        SetMissing(new Vector3I(x, y, z), true);
                    }
                }
            }
        }
    }

    private int BitIndex(Vector3I emplacement)
    {
        return emplacement.X + (emplacement.Y * Width) + (emplacement.Z * Width * Height);
    }

    private IEnumerable<TrixelFace> RebuildVisualFaces()
    {
        var trixelFaces = new List<TrixelFace>();

        var size = new Vector3I(Width, Height, Depth);

        FaceOrientation[] axes = [FaceOrientation.Right, FaceOrientation.Top, FaceOrientation.Front];
        foreach (var orientation in axes)
        {
            var oppositeOrientation = orientation.GetOpposite();

            var direction = orientation.AsIntVector();
            var directionIndexDiff = BitIndex(direction);

            var tangent = Vector3I.Abs(orientation.GetTangent().AsIntVector());
            var bitangent = Vector3I.Abs(orientation.GetBitangent().AsIntVector());

            var tangentLength = Vector3I.Dot(size, tangent);
            var bitangentLength = Vector3I.Dot(size, bitangent);
            var directionLength = Vector3I.Dot(size, direction);

            for (var x = 0; x < tangentLength; x++)
            {
                for (var y = 0; y < bitangentLength; y++)
                {
                    var lineStart = tangent * x + bitangent * y;
                    var initialPositionIndex = BitIndex(tangent * x + bitangent * y);

                    var lastTrixelMissing = true;
                    for (var z = 0; z <= directionLength; z++)
                    {
                        var trixelIndex = initialPositionIndex + directionIndexDiff * z;
                        var trixelMissing = z >= directionLength || IsMissing(trixelIndex);

                        if (lastTrixelMissing != trixelMissing)
                        {
                            var emplacement = lineStart + direction * (trixelMissing ? z - 1 : z);
                            var faceOrientation = trixelMissing ? orientation : oppositeOrientation;
                            trixelFaces.Add(new TrixelFace(emplacement, faceOrientation));
                        }

                        lastTrixelMissing = trixelMissing;
                    }
                }
            }
        }

        return trixelFaces;
    }

    private class Base64Converter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var base64 = reader.GetString();
            return string.IsNullOrEmpty(base64)
                ? Array.Empty<byte>()
                : Convert.FromBase64String(base64);
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(Convert.ToBase64String(value));
        }
    }

    private class CompressConverter : JsonConverter<RTexture2D>
    {
        public override RTexture2D Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var base64 = reader.GetString();
            if (string.IsNullOrEmpty(base64))
            {
                return new RTexture2D();
            }

            var compressed = Convert.FromBase64String(base64);
            using var ms = new MemoryStream(compressed);
            using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return JsonSerializer.Deserialize<RTexture2D>(output.ToArray())!;
        }

        public override void Write(Utf8JsonWriter writer, RTexture2D value, JsonSerializerOptions options)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(value);
            using var ms = new MemoryStream();
            using (var deflate = new DeflateStream(ms, CompressionLevel.Fastest, true))
            {
                deflate.Write(json);
            }

            writer.WriteStringValue(Convert.ToBase64String(ms.ToArray()));
        }
    }
}