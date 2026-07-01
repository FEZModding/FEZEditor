using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class TrileCollisionMesh : ActorComponent
{
    private const float OverlayOversize = 1.025f;

    private readonly List<InstanceData> _instances = new();

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _multiMesh;

    private readonly Rid _material;

    private bool _instancesDirty;

    public TrileCollisionMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _multiMesh = _rendering.MultiMeshCreate();
        _rendering.MultiMeshSetMesh(_multiMesh, _mesh);
        _rendering.InstanceSetMultiMesh(actor.InstanceRid, _multiMesh);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_material);
        _rendering.FreeRid(_multiMesh);
        _rendering.FreeRid(_mesh);
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/TrileCollisionMesh");
        _rendering.MaterialAssignEffect(_material, effect);
        _rendering.MaterialSetCullMode(_material, CullMode.None);
        _rendering.MaterialSetDepthWrite(_material, false);
        _rendering.MaterialSetAlbedo(_material, Color.White with { A = 102 }); // 40%

        foreach (var collision in Enum.GetValues<CollisionType>())
        {
            var texture = content.Load<Texture2D>($"Textures/{collision}");
            _rendering.MaterialShaderSetParam(_material, $"{collision}Texture", texture);
        }

        var quad = MeshSurface.CreateQuad(Vector3.One);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, quad, _material);
    }

    public void AddInstanceData(
        Vector3 position,
        IDictionary<FaceOrientation, CollisionType> collision,
        Vector3 size,
        Vector3 offset,
        byte phi)
    {
        position = Mathz.GetTrileCenter(position, offset, phi);
        size = Mathz.GetTrileTransformedSize(size, phi) * OverlayOversize;

        foreach (var face in FaceExtensions.SidesOnly)
        {
            if (!collision.TryGetValue(face, out var type))
            {
                throw new KeyNotFoundException($"Missing {face} face");
            }

            _instances.Add(new InstanceData(position, size, face.RotateBy(phi), type));
            _instancesDirty = true;
        }
    }

    public void ClearInstanceData()
    {
        _instances.Clear();
        _instancesDirty = true;
    }

    public override void Update(GameTime gameTime)
    {
        if (_instancesDirty)
        {
            _rendering.MultiMeshAllocate(_multiMesh, _instances.Count, MultiMeshDataType.Matrix);
            _instancesDirty = false;

            for (var i = 0; i < _instances.Count; i++)
            {
                var data = _instances[i].ToStride();
                _rendering.MultiMeshSetInstanceMatrix(_multiMesh, i, data);
            }
        }
    }

    private readonly record struct InstanceData(
        Vector3 Position,
        Vector3 Size,
        FaceOrientation Face,
        CollisionType CollisionType)
    {
        public Matrix ToStride()
        {
            var rotation = Face.AsQuaternion();
            return new Matrix(
                Position.X, Position.Y, Position.Z, 0f,
                rotation.X, rotation.Y, rotation.Z, rotation.W,
                Size.X, Size.Y, Size.Z, (float)CollisionType,
                0f, 0f, 0f, 0f
            );
        }
    }
}