using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class VolumeMesh : ActorComponent, IPickable
{
    private const float OverlayOversize = 1.025f;

    public Vector3 Size { get; set; } = Vector3.One;

    public Color Color { get; set; } = Color.White;

    public bool Pickable { get; set; } = true;

    public bool IsBlackHole { get; set; }

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _material;

    private readonly Rid _overlay;

    private Texture2D _volumeTexture = null!;

    private Texture2D _starsTexture = null!;

    internal VolumeMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _overlay = _rendering.MaterialCreate();
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
    }

    public override void LoadContent(IContentManager content)
    {
        _volumeTexture = content.Load<Texture2D>("Textures/Volume");
        _starsTexture = content.Load<Texture2D>("Textures/Stars");

        _rendering.MaterialAssignEffect(_material, _rendering.BasicEffectVertexColor);
        _rendering.MaterialSetCullMode(_material, CullMode.None);

        _rendering.MaterialAssignEffect(_overlay, _rendering.BasicEffect);
        _rendering.MaterialSetCullMode(_overlay, CullMode.None);
        _rendering.MaterialSetSamplerState(_overlay, SamplerState.PointWrap);
        _rendering.MaterialSetDepthWrite(_overlay, false);
    }

    public override void Update(GameTime gameTime)
    {
        if (IsBlackHole)
        {
            var surface = MeshSurface.CreateTexturedBox(Size);
            var wire = MeshSurface.CreateWireframeBox(Size, Color);
            _rendering.MeshClear(_mesh);
            _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _overlay);
            _rendering.MeshAddSurface(_mesh, PrimitiveType.LineList, wire, _material);
            _rendering.MaterialAssignBaseTexture(_overlay, _starsTexture);
            _rendering.MaterialSetAlbedo(_overlay, Color.White);
        }
        else
        {
            var surface = MeshSurface.CreateTexturedBox(Size * OverlayOversize);
            var wire = MeshSurface.CreateWireframeBox(Size * OverlayOversize, Color);
            _rendering.MeshClear(_mesh);
            _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _overlay);
            _rendering.MeshAddSurface(_mesh, PrimitiveType.LineList, wire, _material);
            _rendering.MaterialAssignBaseTexture(_overlay, _volumeTexture);
            _rendering.MaterialSetBlendMode(_overlay, BlendMode.AlphaBlend);
            _rendering.MaterialSetAlbedo(_overlay, Color with { A = 102 }); // 40%
        }
    }

    public IEnumerable<BoundingBox> GetBounds()
    {
        var half = Size / 2f;
        var position = Actor.Transform.Position;
        yield return new BoundingBox(position - half, position + half);
    }

    public PickHit? Pick(Ray ray)
    {
        var box = GetBounds().First();
        if (box.Contains(ray.Position) != ContainmentType.Disjoint)
        {
            return null;
        }

        var dist = ray.Intersects(box);
        return dist.HasValue ? new PickHit(dist.Value, 0) : null;
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_overlay);
        _rendering.FreeRid(_material);
        _rendering.FreeRid(_mesh);
    }
}