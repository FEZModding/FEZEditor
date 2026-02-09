using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Hosts;

public class ArtObjectHost : Host
{
    public sealed override Rid Rid { get; protected set; }
    
    public string Name { get; private set; } = "";

    public Vector3 Position { get; set; } = Vector3.Zero;

    public Quaternion Rotation { get; set; } = Quaternion.Identity;

    public Vector3 Scale { get; set; } = Vector3.One;
    
    public Vector3 Size { get; set; } = Vector3.One;

    public BoundingBox Bounds { get; private set; }

    private Rid _mesh;

    private Rid _material;
    
    public ArtObjectHost(Game game) : base(game)
    {
        Rid = RenderingService.InstanceCreate(Rid.Invalid);
    }

    ~ArtObjectHost()
    {
        Dispose();
    }

    public override void Load(object asset)
    {
        if (asset is not ArtObject ao)
        {
            return;
        }
        
        Name = ao.Name;
        Size = ao.Size.ToXna();
        
        var effect = Game.Content.Load<Effect>("Effects/ArtObject");
        _material = RenderingService.MaterialCreate(effect);
        RenderingService.MaterialAssignBaseTexture(_material, ao.Cubemap.ToXna(RenderingService.GraphicsDevice));
        RenderingService.MaterialSetBlendMode(_material, BlendMode.AlphaBlend);
        RenderingService.MaterialSetCullMode(_material, CullMode.CullCounterClockwiseFace);
        RenderingService.MaterialSetSamplerState(_material, SamplerState.PointClamp);
        
        _mesh = RenderingService.MeshCreate();
        RenderingService.MeshAddSurface(_mesh, PrimitiveType.TriangleList, ao.Geometry.ToXna(), _material);
        RenderingService.InstanceSetMesh(Rid, _mesh);
    }

    public override void Update(GameTime gameTime)
    {
        Bounds = Mathz.ComputeBoundingBox(Position, Rotation, Scale, Size);
        RenderingService.InstanceSetPosition(Rid, Position);
        RenderingService.InstanceSetRotation(Rid, Rotation);
        RenderingService.InstanceSetScale(Rid, Scale);
    }

    public override void Dispose()
    {
        RenderingService.FreeRid(_material);
        RenderingService.FreeRid(_mesh);
        base.Dispose();
    }
}