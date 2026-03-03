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
    private const float ZFightingOffset = 1.01f;

    private static readonly FaceOrientation[] Sides = new[]
    {
        FaceOrientation.Front,
        FaceOrientation.Right,
        FaceOrientation.Back,
        FaceOrientation.Left
    };

    public bool Visible
    {
        get => _rendering.InstanceIsVisible(_instance);
        set => _rendering.InstanceSetVisibility(_instance, value);
    }

    private readonly RenderingService _rendering;

    private readonly Rid _instance;

    private readonly Rid _mesh;

    private readonly Dictionary<FaceOrientation, Rid> _materials = new();

    private readonly Dictionary<CollisionType, Texture2D> _textures = new();

    public TrileCollisionMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _instance = _rendering.InstanceCreate(actor.InstanceRid);
        _mesh = _rendering.MeshCreate();
        _rendering.InstanceSetMesh(_instance, _mesh);
        _rendering.InstanceSetVisibility(_instance, false);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var material in _materials.Values)
        {
            _rendering.FreeRid(material);
        }

        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_instance);
    }

    public override void LoadContent(IContentManager content)
    {
        foreach (var face in Sides)
        {
            var effect = new BasicEffect(_rendering.GraphicsDevice);
            var material = _rendering.MaterialCreate();
            _rendering.MaterialAssignEffect(material, effect);
            _rendering.MaterialSetCullMode(material, CullMode.None);
            _rendering.MaterialSetAlbedo(material, Color.White with { A = 204 }); // 80%
            _materials[face] = material;
        }

        foreach (var collision in Enum.GetValues<CollisionType>())
        {
            _textures[collision] = content.Load<Texture2D>($"Textures/{collision}");
        }
    }

    public void Visualize(Dictionary<FaceOrientation, CollisionType> collisionFaces, Vector3 size)
    {
        _rendering.InstanceSetPosition(_instance, size / 2f);
        _rendering.MeshClear(_mesh);

        foreach (var (face, collision) in collisionFaces)
        {
            var surface = MeshSurface.CreateFaceQuad(size * ZFightingOffset, face);
            _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _materials[face]);
            _rendering.MaterialAssignBaseTexture(_materials[face], _textures[collision]);
        }
    }
}