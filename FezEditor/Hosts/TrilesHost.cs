using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Hosts;

public class TrilesHost : Host
{
    public class Instance
    {
        public int Id { get; set; } = -1;

        public Vector3 Position { get; set; } = Vector3.Zero;

        public TrileRotation Rotation { get; set; } = TrileRotation.Front;
        
        public Vector3 Size { get; set; } = Vector3.One;
    
        public BoundingBox Bounds { get; set; }
    }
    
    private const int InstanceCount = 200;
    
    public sealed override Rid Rid { get; protected set; }

    public IReadOnlyDictionary<TrileEmplacement, Instance> Triles => _triles;
    
    private readonly Dictionary<TrileEmplacement, Instance> _triles = new();

    private readonly Dictionary<int, MultiInstance> _multiInstances = new();

    private bool _instancesDirty;
    
    public TrilesHost(Game game) : base(game)
    {
        Rid = RenderingService.InstanceCreate(Rid.Invalid);
    }

    ~TrilesHost()
    {
        Dispose();
    }

    public void AddTrile(TrileEmplacement emplacement, Instance instance)
    {
        _instancesDirty = _triles.TryAdd(emplacement, instance);
    }

    public void ClearTrile(TrileEmplacement emplacement)
    {
        _instancesDirty = _triles.Remove(emplacement);
    }
    
    public override void Load(object asset)
    {
        if (asset is not TrileSet trileSet)
        {
            return;
        }
        
        var effect = Game.Content.Load<Effect>("Effects/TrileEffect");
        var texture = trileSet.TextureAtlas.ToXna(RenderingService.GraphicsDevice);
        
        foreach (var (id, trile) in trileSet.Triles)
        {
            var material = RenderingService.MaterialCreate(effect);
            RenderingService.MaterialAssignBaseTexture(material, texture);
            RenderingService.MaterialSetCullMode(material, CullMode.CullCounterClockwiseFace);
            RenderingService.MaterialSetSamplerState(material, SamplerState.LinearClamp);
            
            var mesh = RenderingService.MeshCreate();
            RenderingService.MeshAddSurface(mesh, PrimitiveType.TriangleList, trile.Geometry.ToXna(), material);
            
            var multiMesh = RenderingService.MultiMeshCreate();
            RenderingService.MultiMeshAllocate(multiMesh, InstanceCount, MultiMeshDataType.Vector4);
            RenderingService.MultiMeshSetMesh(multiMesh, mesh);
            
            var instance = RenderingService.InstanceCreate(Rid);
            RenderingService.InstanceSetMultiMesh(multiMesh, instance);
            
            _multiInstances[id] = new MultiInstance(instance, mesh, multiMesh, material, trile.Size.ToXna());
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (!_instancesDirty)
        {
            return;
        }
        
        var multiMeshIds = new Dictionary<int, List<Vector4>>();
        foreach (var instance in Triles.Values)
        {
            if (!multiMeshIds.TryGetValue(instance.Id, out var datas))
            {
                datas = new List<Vector4>();
                multiMeshIds.Add(instance.Id, datas);
            }
            
            var data = new Vector4(instance.Position, instance.Rotation.AsPhi());
            datas.Add(data);
        }
        
        foreach (var (id, datas) in multiMeshIds)
        {
            var visual = _multiInstances[id];
            RenderingService.MultiMeshSetVisibleInstances(visual.MultiMesh, datas.Count);
            for (var i = 0; i < datas.Count; i++)
            {
                RenderingService.MultiMeshSetInstanceVector4(visual.MultiMesh, i, datas[i]);
            }
        }

        foreach (var instance in Triles.Values)
        {
            instance.Bounds = Mathz.ComputeBoundingBox(
                instance.Position, 
                instance.Rotation.AsQuaternion(), 
                Vector3.One, 
                instance.Size
            );
        }
    }

    public override void Dispose()
    {
        foreach (var multiInstance in _multiInstances.Values)
        {
            RenderingService.FreeRid(multiInstance.Material);
            RenderingService.FreeRid(multiInstance.MultiMesh);
            RenderingService.FreeRid(multiInstance.Mesh);
            RenderingService.FreeRid(multiInstance.Base);
        }
        base.Dispose();
    }

    private record struct MultiInstance(Rid Base, Rid Mesh, Rid MultiMesh, Rid Material, Vector3 Size);
}