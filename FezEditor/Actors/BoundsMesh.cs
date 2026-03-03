using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class BoundsMesh : ActorComponent
{
    private static readonly Color WireColor = new(1f, 1f, 1f, 0.5f);

    private readonly RenderingService _rendering;

    private readonly Rid _instance;

    private readonly Rid _mesh;

    private Rid _material;

    public BoundsMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _instance = _rendering.InstanceCreate(actor.InstanceRid);
        _mesh = _rendering.MeshCreate();
        _rendering.InstanceSetMesh(_instance, _mesh);
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = new BasicEffect(_rendering.GraphicsDevice) { VertexColorEnabled = true };
        _material = _rendering.MaterialCreate();
        _rendering.MaterialAssignEffect(_material, effect);
        _rendering.MaterialSetCullMode(_material, CullMode.None);
    }

    public void Visualize(Vector3 size)
    {
        _rendering.InstanceSetPosition(_instance, size / 2f);
        _rendering.MeshClear(_mesh);
        var surface = MeshSurface.CreateWireframeBox(size, WireColor);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.LineList, surface, _material);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_material);
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_instance);
    }
}