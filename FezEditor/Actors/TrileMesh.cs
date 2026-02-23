using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class TrileMesh : ActorComponent
{
    private const int MaxInstancesCount = 200;
    
    public int VisibleCount { get; set; }

    private readonly OrderedDictionary<TrileEmplacement, InstanceData> _instances = new();

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _multiMesh;

    private readonly Rid _material;
    
    private Vector3 _size;

    internal TrileMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _multiMesh = _rendering.MeshCreate();
        _rendering.MultiMeshSetMesh(_multiMesh, _mesh);
        _rendering.InstanceSetMultiMesh(actor.InstanceRid, _multiMesh);
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/Trile");
        _rendering.MaterialAssignEffect(_material, effect);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_multiMesh);
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
    }

    public void Visualize(TrileSet trileSet, int id)
    {
        var texture = RepackerExtensions.ConvertToTexture2D(trileSet.TextureAtlas);
        _rendering.MaterialAssignBaseTexture(_material, texture);

        var trile = trileSet.Triles[id];
        var surface = RepackerExtensions.ConvertToMesh(trile.Geometry.Vertices, trile.Geometry.Indices); 
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
        
        _rendering.MultiMeshAllocate(_multiMesh, MaxInstancesCount, MultiMeshDataType.Vector4);
        _size = trileSet.Triles[id].Size.ToXna();
    }

    public void SetInstancePosition(TrileEmplacement emplacement, Vector3 position)
    {
        var instance = _instances[emplacement];
        instance.Position = position;
        _instances[emplacement] = instance;
    }

    public void SetInstanceRotation(TrileEmplacement emplacement, TrileRotation rotation)
    {
        var instance = _instances[emplacement];
        instance.Rotation = rotation;
        _instances[emplacement] = instance;
    }

    public BoundingBox GetInstanceCollider(TrileEmplacement emplacement)
    {
        var position = _instances[emplacement].Position;
        var rotation = _instances[emplacement].Rotation.AsQuaternion();
        return Mathz.ComputeBoundingBox(position, rotation, Vector3.One, _size);
    }
    
    public override void Update(GameTime gameTime)
    {
        _rendering.MultiMeshSetVisibleInstances(_multiMesh, VisibleCount);
        for (var i = 0; i < _instances.Count; i++)
        {
            var data = _instances.GetAt(i).Value.ToStride();
            _rendering.MultiMeshSetInstanceVector4(_multiMesh, i, data);
        }
    }

    private record struct InstanceData(Vector3 Position, TrileRotation Rotation)
    {
        public Vector4 ToStride() => new(Position, Rotation.AsPhi());
    }
}