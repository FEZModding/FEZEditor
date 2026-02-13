using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class ArtObjectMesh : ActorComponent
{
    private RenderingService _rendering = null!;
    
    private Rid _mesh;

    private Rid _material;

    public override void Initialize()
    {
        _rendering = Game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
    }

    public void Load(ArtObject ao)
    {
        var texture = ao.Cubemap.ToXna(_rendering.GraphicsDevice);
        _rendering.MaterialAssignBaseTexture(_material, texture);
        
        var effect = Game.Content.Load<Effect>("Effects/ArtObject");
        _rendering.MaterialAssignEffect(_material, effect);

        var surface = ao.Geometry.ToXna();
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
        _rendering.InstanceSetMesh(Actor.InstanceRid, _mesh);
    }

    public override void Dispose()
    {
        _rendering.InstanceSetMesh(Actor.InstanceRid, Rid.Invalid);
        _rendering.MeshClear(_mesh);
        _rendering.MaterialReset(_material);
    }
}