using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Graphics;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PrimitiveType = FEZRepacker.Core.Definitions.Game.XNA.PrimitiveType;

namespace FezEditor.Components;

public class TrileSubject : ITrixelSubject
{
    private const int TextureWidth = 108;

    private const int TextureHeight = 18;

    private const int AtlasWidth = 1024;

    private const int AtlasStartingHeight = 32;

    private const int AtlasColumns = AtlasWidth / TextureWidth;

    public string TextureExportKey => $"{_set.Name}#{_id}";

    public int Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                if (!_set.Triles.ContainsKey(_id))
                {
                    throw new KeyNotFoundException($"Trile with ID {_id} not found");
                }

                _id = value;
            }
        }
    }

    public string Name
    {
        get => _set.Name;
        set => _set.Name = value;
    }

    private Trile Trile => _set.Triles[_id];

    private readonly TrileSet _set;

    private readonly Game _game;

    private readonly Texture2D _missing;

    private readonly Dictionary<int, byte[]> _pendingTextures = new();

    private Texture2D? _atlasTexture;

    private Texture2D? _currentTexture;

    private int _id;

    private Action<Vector3>? _resized;

    public TrileSubject(TrileSet set, Game game)
    {
        _set = set;
        _game = game;
        _missing = game.Content.Load<Texture2D>("Missing");
        _id = set.Triles
            .OrderBy(kv => kv.Key)
            .First(kv => kv.Value.Geometry.Vertices.Length > 0)
            .Key;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _currentTexture?.Dispose();
        _currentTexture = null;
        _atlasTexture?.Dispose();
        _atlasTexture = null;
    }

    public TrixelObject Materialize()
    {
        var obj = TrixelMaterializer.ReconstructGeometry(Trile.Size.ToXna(), Trile.Geometry.Vertices, Trile.Geometry.Indices);
        _resized = obj.Resize;
        return obj;
    }

    public object GetAsset(TrixelObject obj)
    {
        #region Apply New Properties
        
        var trile = new Trile
        {
            Size = obj.Size.ToRepacker(),
            Name = Trile.Name,
            Offset = Trile.Offset,
            AtlasOffset = Trile.AtlasOffset,
            Immaterial = Trile.Immaterial,
            SeeThrough = Trile.SeeThrough,
            Thin = Trile.Thin,
            ForceHugging = Trile.ForceHugging,
            Type = Trile.Type,
            Face = Trile.Face,
            SurfaceType = Trile.SurfaceType,
            Faces = new Dictionary<FaceOrientation, CollisionType>(Trile.Faces),
        };

        (trile.Geometry.Vertices, trile.Geometry.Indices) = TrixelMaterializer.Dematerialize(obj);
        _set.Triles[Id] = trile;
        
        #endregion

        #region Rebuild Atlas Texture

        RebuildAtlas(_set, _pendingTextures);
        _atlasTexture?.Dispose();
        _atlasTexture = new Texture2D(_game.GraphicsDevice, _set.TextureAtlas.Width, _set.TextureAtlas.Height, false, SurfaceFormat.Color);
        _atlasTexture.SetData(_set.TextureAtlas.TextureData);
        RepackerExtensions.SetAlpha(_atlasTexture, 1f);

        #endregion
        
        #region Apply Atlas Offset to TexCoords

        var scaleUv = new RVector2
        {
            X = (float)TextureWidth / _set.TextureAtlas.Width,
            Y = (float)TextureHeight / _set.TextureAtlas.Height
        };
        
        foreach (var trile1 in _set.Triles.Values)
        {
            foreach (var vertex in trile1.Geometry.Vertices)
            {
                vertex.TextureCoordinate = new RVector2(
                    trile1.AtlasOffset.X + vertex.TextureCoordinate.X * scaleUv.X,
                    trile1.AtlasOffset.Y + vertex.TextureCoordinate.Y * scaleUv.Y
                );
            }
        }
        
        #endregion
        
        return _set;
    }

    public Texture2D LoadTexture()
    {
        if (_atlasTexture == null)
        {
            var atlas = _set.TextureAtlas;
            _atlasTexture = new Texture2D(_game.GraphicsDevice, atlas.Width, atlas.Height, false, SurfaceFormat.Color);
            _atlasTexture.SetData(atlas.TextureData);
            RepackerExtensions.SetAlpha(_atlasTexture, 1f);
        }

        _currentTexture?.Dispose();
        _currentTexture = SliceTexture(_id);
        return _currentTexture;
    }

    public void UpdateTexture(Texture2D texture)
    {
        var pixels = new byte[texture.Width * texture.Height * 4];
        texture.GetData(pixels);
        _pendingTextures[_id] = pixels;

        _currentTexture?.Dispose();
        _currentTexture = texture;
    }

    private Texture2D SliceTexture(int id)
    {
        var trile = _set.Triles[id];
        var atlas = _set.TextureAtlas;
        var px = (int)MathF.Round(trile.AtlasOffset.X * atlas.Width);
        var py = (int)MathF.Round(trile.AtlasOffset.Y * atlas.Height);

        byte[] srcData;
        if (_pendingTextures.TryGetValue(id, out var pending))
        {
            srcData = pending;
        }
        else
        {
            srcData = new byte[TextureWidth * TextureHeight * 4];
            for (var row = 0; row < TextureHeight; row++)
            {
                var src = ((py + row) * atlas.Width + px) * 4;
                Buffer.BlockCopy(atlas.TextureData, src, srcData, row * TextureWidth * 4, TextureWidth * 4);
            }
        }

        var tex2D = new Texture2D(_game.GraphicsDevice, TextureWidth, TextureHeight, false, SurfaceFormat.Color);
        tex2D.SetData(srcData);
        RepackerExtensions.SetAlpha(tex2D, 1f);
        return tex2D;
    }

    public void DrawProperties(History history)
    {
        var name = Trile.Name;
        if (ImGui.InputText("Name", ref name, 255))
        {
            using (history.BeginScope("Edit Name")) 
            {
                Trile.Name = name;
            }
        }
        
        var size = Trile.Size.ToXna();
        if (ImGuiX.DragFloat3("Size", ref size))
        {
            using (history.BeginScope("Edit Size")) 
            {
                Trile.Size = size.ToRepacker();
                _resized?.Invoke(size);
            }
        }
        
        var offset = Trile.Offset.ToXna();
        if (ImGuiX.DragFloat3("Offset", ref offset))
        {
            using (history.BeginScope("Edit Offset")) 
            {
                Trile.Offset = offset.ToRepacker();
            }
        }
        
        var immaterial = Trile.Immaterial;
        if (ImGui.Checkbox("Immaterial", ref immaterial))
        {
            using (history.BeginScope("Edit Immaterial")) 
            {
                Trile.Immaterial = immaterial;
            }
        }
        
        var seeThrough = Trile.SeeThrough;
        if (ImGui.Checkbox("See Through", ref seeThrough))
        {
            using (history.BeginScope("Edit See Through")) 
            {
                Trile.SeeThrough = seeThrough;
            }
        }
        
        var thin = Trile.Thin;
        if (ImGui.Checkbox("Thin", ref thin))
        {
            using (history.BeginScope("Edit Thin")) 
            {
                Trile.Thin = thin;
            }
        }
        
        var forceHugging = Trile.ForceHugging;
        if (ImGui.Checkbox("Force Hugging", ref forceHugging))
        {
            using (history.BeginScope("Edit Force Hugging")) 
            {
                Trile.ForceHugging = forceHugging;
            }
        }
        
        var actorType = (int)Trile.Type;
        var actorTypes = Enum.GetNames<ActorType>();
        if (ImGui.Combo("Actor Type", ref actorType, actorTypes, actorTypes.Length))
        {
            using (history.BeginScope("Edit Actor Type"))
            {
                Trile.Type = (ActorType)actorType;
            }
        }
        
        var actorFace = (int)Trile.Face;
        var actorFaces = Enum.GetNames<ActorType>();
        if (ImGui.Combo("Actor Face", ref actorFace, actorFaces, actorFaces.Length))
        {
            using (history.BeginScope("Edit Actor Face")) 
            {
                Trile.Face = (FaceOrientation)actorFace;
            }
        }
        
        var surfaceType = (int)Trile.SurfaceType;
        var surfaceTypes = Enum.GetNames<SurfaceType>();
        if (ImGui.Combo("Surface Type", ref surfaceType, surfaceTypes, surfaceTypes.Length))
        {
            using (history.BeginScope("Edit Surface Type")) 
            {
                Trile.SurfaceType = (SurfaceType)surfaceType;
            }
        }
        
        // FaceOrientation is not IEquatable, so string key is being used
        var collisionFaces = Trile.Faces.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
        if (ImGuiX.EditableDict("Collision Faces", ref collisionFaces, RenderFace, AddCollisionType, () => CollisionType.None))
        {
            using (history.BeginScope("Edit Collision Faces")) 
            {
                Trile.Faces = collisionFaces.ToDictionary(kv => Enum.Parse<FaceOrientation>(kv.Key), kv => kv.Value);
            }
        }
    }

    private static bool RenderFace(string key, ref CollisionType value)
    {
        ImGui.Text(key + ":");
        ImGui.SameLine();
        var collisionType = (int)value;
        var collisionTypes = Enum.GetNames<CollisionType>();
        return ImGui.Combo($"##{key}_value", ref collisionType, collisionTypes, collisionTypes.Length);
    }

    private static bool AddCollisionType(ref string key)
    {
        if (string.IsNullOrEmpty(key)) key = nameof(FaceOrientation.Left);
        var face = (int)Enum.Parse<FaceOrientation>(key);
        var faces = Enum.GetNames<FaceOrientation>();
        return ImGui.Combo("##item", ref face, faces, faces.Length);
    }

    public IEnumerable<Entry> EnumerateEntries(string filter = "")
    {
        var ids = _set.Triles.Keys.ToArray();
        foreach (var id in ids)
        {
            var trile = _set.Triles[id];
            if (!trile.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (trile.Geometry.Vertices.Length < 1)
            {
                yield return new Entry(id, trile.Name, _missing, Vector2.Zero, Vector2.One);
                continue;
            }

            // Select front face side of trile withing the atlas for performance
            var atlasHeight = _atlasTexture?.Height ?? _set.TextureAtlas.Height;
            var uv0 = new Vector2(trile.AtlasOffset.X, trile.AtlasOffset.Y);
            var uv1 = new Vector2(
                trile.AtlasOffset.X + TextureWidth / (AtlasWidth * 6f),
                trile.AtlasOffset.Y + (float)TextureHeight / atlasHeight
            );

            yield return new Entry(id, trile.Name, _atlasTexture ?? _missing, uv0, uv1);
        }
    }

    private static void RebuildAtlas(TrileSet set, Dictionary<int, byte[]> overrides)
    {
        var rows = (int)MathF.Ceiling((float)set.Triles.Count / AtlasColumns);
        var requiredHeight = rows * TextureHeight;
        
        var atlasHeight = AtlasStartingHeight;
        while (atlasHeight < requiredHeight)
        {
            atlasHeight <<= 1;
        }

        var atlasData = new byte[AtlasWidth * atlasHeight * 4];
        var i = 0;

        foreach (var (id, trile) in set.Triles)
        {
            var col = i % AtlasColumns;
            var row = i / AtlasColumns;
            var destX = col * TextureWidth;
            var destY = row * TextureHeight;

            byte[] srcData;
            if (overrides.TryGetValue(id, out var pending))
            {
                srcData = pending;
            }
            else if (set.TextureAtlas.TextureData.Length > 0)
            {
                var atlas = set.TextureAtlas;
                var px = (int)MathF.Round(trile.AtlasOffset.X * atlas.Width);
                var py = (int)MathF.Round(trile.AtlasOffset.Y * atlas.Height);
                srcData = new byte[TextureWidth * TextureHeight * 4];
                for (var srcRow = 0; srcRow < TextureHeight; srcRow++)
                {
                    var src = ((py + srcRow) * atlas.Width + px) * 4;
                    Buffer.BlockCopy(atlas.TextureData, src, srcData, srcRow * TextureWidth * 4, TextureWidth * 4);
                }
            }
            else
            {
                srcData = new byte[TextureWidth * TextureHeight * 4];
            }

            for (var srcRow = 0; srcRow < TextureHeight; srcRow++)
            {
                Buffer.BlockCopy(srcData, srcRow * TextureWidth * 4,
                    atlasData, ((destY + srcRow) * AtlasWidth + destX) * 4,
                    TextureWidth * 4);
            }

            trile.AtlasOffset = new RVector2((float)destX / AtlasWidth, (float)destY / atlasHeight);
            i++;
        }

        set.TextureAtlas = new RTexture2D
        {
            Width = AtlasWidth,
            Height = atlasHeight,
            TextureData = atlasData
        };
    }

    public int AddTrile()
    {
        var newId = _set.Triles.Count > 0 
            ? _set.Triles.Keys.Max() + 1 
            : 0;
        
        var newTrile = new Trile
        {
            Name = "UNTITLED",
            Size = Vector3.One.ToRepacker()
        };
        
        _set.Triles[newId] = newTrile;
        _pendingTextures[newId] = new byte[TextureWidth * TextureHeight * 4];
        return newId;
    }

    public int RemoveTriles(HashSet<int> ids)
    {
        foreach (var id in ids)
        {
            _set.Triles.Remove(id);
            _pendingTextures.Remove(id);
        }

        if (_set.Triles.Count == 0)
        {
            return AddTrile();
        }

        // Return last remaining id before the removed range, or first available
        var remaining = _set.Triles.Keys.OrderBy(k => k).ToList();
        return remaining.LastOrDefault(k => k < ids.Min(), remaining[0]);
    }

    public int CopyTriles(IEnumerable<int> sourceIds)
    {
        var lastId = -1;
        foreach (var sourceId in sourceIds)
        {
            var source = _set.Triles[sourceId];
            var newId = _set.Triles.Keys.Max() + 1;

            _set.Triles[newId] = new Trile
            {
                Name = source.Name,
                Size = source.Size,
                Offset = source.Offset,
                AtlasOffset = source.AtlasOffset,
                Immaterial = source.Immaterial,
                SeeThrough = source.SeeThrough,
                Thin = source.Thin,
                ForceHugging = source.ForceHugging,
                Type = source.Type,
                Face = source.Face,
                SurfaceType = source.SurfaceType,
                Faces = new Dictionary<FaceOrientation, CollisionType>(source.Faces),
                Geometry = new IndexedPrimitives<VertexInstance, RVector4>
                {
                    PrimitiveType = PrimitiveType.TriangleList,
                    Vertices = source.Geometry.Vertices.ToArray(),
                    Indices = source.Geometry.Indices.ToArray()
                }
            };

            if (_pendingTextures.TryGetValue(sourceId, out var srcPixels))
            {
                var copyPixels = new byte[srcPixels.Length];
                Buffer.BlockCopy(srcPixels, 0, copyPixels, 0, srcPixels.Length);
                _pendingTextures[newId] = copyPixels;
            }
            else
            {
                var atlas = _set.TextureAtlas;
                var px = (int)MathF.Round(source.AtlasOffset.X * atlas.Width);
                var py = (int)MathF.Round(source.AtlasOffset.Y * atlas.Height);
                var pixels = new byte[TextureWidth * TextureHeight * 4];
                for (var row = 0; row < TextureHeight; row++)
                {
                    var src = ((py + row) * atlas.Width + px) * 4;
                    Buffer.BlockCopy(atlas.TextureData, src, pixels, row * TextureWidth * 4, TextureWidth * 4);
                }
                _pendingTextures[newId] = pixels;
            }

            lastId = newId;
        }
        
        return lastId;
    }

    public void AppendTriles(IEnumerable<int> ids, TrileSet targetSet)
    {
        // Collect pixel data for selected triles before modifying anything
        var slices = new List<(Trile Trile, byte[] Pixels)>();
        foreach (var id in ids)
        {
            if (!_set.Triles.TryGetValue(id, out var trile))
            {
                continue;
            }

            byte[] pixels;
            if (_pendingTextures.TryGetValue(id, out var pending))
            {
                pixels = pending;
            }
            else
            {
                var atlas = _set.TextureAtlas;
                var px = (int)MathF.Round(trile.AtlasOffset.X * atlas.Width);
                var py = (int)MathF.Round(trile.AtlasOffset.Y * atlas.Height);
                pixels = new byte[TextureWidth * TextureHeight * 4];
                for (var row = 0; row < TextureHeight; row++)
                {
                    var src = ((py + row) * atlas.Width + px) * 4;
                    Buffer.BlockCopy(atlas.TextureData, src, pixels, row * TextureWidth * 4, TextureWidth * 4);
                }
            }

            slices.Add((trile, pixels));
        }

        // Assign new non-conflicting IDs in the target set
        var nextId = targetSet.Triles.Count > 0 ? targetSet.Triles.Keys.Max() + 1 : 0;
        foreach (var (trile, _) in slices)
        {
            var copy = new Trile
            {
                Name = trile.Name,
                Size = trile.Size,
                Offset = trile.Offset,
                Immaterial = trile.Immaterial,
                SeeThrough = trile.SeeThrough,
                Thin = trile.Thin,
                ForceHugging = trile.ForceHugging,
                Type = trile.Type,
                Face = trile.Face,
                SurfaceType = trile.SurfaceType,
                Faces = new Dictionary<FaceOrientation, CollisionType>(trile.Faces),
            };
            (copy.Geometry.Vertices, copy.Geometry.Indices) =
                (trile.Geometry.Vertices.ToArray(), trile.Geometry.Indices.ToArray());
            targetSet.Triles[nextId++] = copy;
        }

        // Build pixel overrides: newly appended triles keyed by their new IDs
        var appendStart = nextId - slices.Count;
        var overrides = new Dictionary<int, byte[]>();
        for (var j = 0; j < slices.Count; j++)
        {
            overrides[appendStart + j] = slices[j].Pixels;
        }

        RebuildAtlas(targetSet, overrides);
    }

    public readonly record struct Entry(int Id, string Name, Texture2D Texture, Vector2 Uv0, Vector2 Uv1);
}