using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class ArtObjectMesh : ActorComponent, IPickable, ITinted
{
    public Color Tint { get; set; } = Color.Transparent;

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _material;

    private Texture2D? _texture;

    private Vector3 _size;

    private Vector3[] _vertices = [];

    private int[] _indices = [];

    internal ArtObjectMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/ArtObjectMesh");
        _rendering.MaterialAssignEffect(_material, effect);
    }

    public void Visualize(ArtObject ao)
    {
        _texture?.Dispose();

        _size = ao.Size.ToXna();

        if (ao.Cubemap != null)
        {
            _texture = RepackerExtensions.ConvertToTexture2D(ao.Cubemap);
            _rendering.MaterialAssignBaseTexture(_material, _texture);
        }
        else
        {
            _texture = null;
        }

        _rendering.MeshClear(_mesh);
        if (ao.Geometry != null)
        {
            var surface = RepackerExtensions.ConvertToMesh(ao.Geometry.Vertices, ao.Geometry.Indices);
            _vertices = surface.Vertices;
            _indices = surface.Indices;
            _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
        }
        else
        {
            _vertices = [];
            _indices = [];
        }
    }

    public override void Update(GameTime gameTime)
    {
        _rendering.MaterialShaderSetParam<Vector4>(_material, "Tint", Tint.ToVector4());
    }

    public IEnumerable<BoundingBox> GetBounds()
    {
        yield return Mathz.ComputeBoundingBox(
            Actor.Transform.Position, Actor.Transform.Rotation, Actor.Transform.Scale, _size);
    }

    public PickHit? Pick(Ray ray)
    {
        var box = GetBounds().First();
        if (!ray.Intersects(box).HasValue)
        {
            return null;
        }

        var localRay = Actor.Transform.TransformRay(ray);
        var minDist = float.MaxValue;
        for (var i = 0; i + 2 < _indices.Length; i += 3)
        {
            var t = localRay.IntersectsTriangle(
                _vertices[_indices[i]],
                _vertices[_indices[i + 1]],
                _vertices[_indices[i + 2]]
            );

            if (t < minDist)
            {
                minDist = t.Value;
            }
        }

        return minDist < float.MaxValue ? new PickHit(minDist, 0) : null;
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _texture?.Dispose();
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
    }
}