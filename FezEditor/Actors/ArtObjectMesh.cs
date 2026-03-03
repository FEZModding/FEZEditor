using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class ArtObjectMesh : ActorComponent
{
    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _material;

    internal ArtObjectMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/ArtObject");
        _rendering.MaterialAssignEffect(_material, effect);
    }

    public void Visualize(ArtObject ao)
    {
        var texture = RepackerExtensions.ConvertToTexture2D(ao.Cubemap);
        _rendering.MaterialAssignBaseTexture(_material, texture);

        var surface = RepackerExtensions.ConvertToMesh(ao.Geometry.Vertices, ao.Geometry.Indices);
        _rendering.MeshClear(_mesh);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
    }
}