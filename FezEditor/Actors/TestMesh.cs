using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class TestMesh : ActorComponent
{
    private RenderingService _rendering = null!;
    
    private Rid _mesh;

    private Rid _material;
    
    private float _elapsed;
    
    public override void Initialize()
    {
        _rendering = Game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
    }

    public void Load()
    {
        _rendering.MaterialSetCullMode(_material, CullMode.None);
        
        var surface = new MeshSurface
        {
            Vertices = new[]
            {
                new Vector3(0.0f, 0.5f, 0f), // top
                new Vector3(0.5f, -0.5f, 0f), // bottom-right
                new Vector3(-0.5f, -0.5f, 0f) // bottom-left
            },
            Indices = new[] { 0, 1, 2 },
            Colors = new[]
            {
                Color.Red,
                Color.Green,
                Color.Blue
            }
        };
        
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
        _rendering.InstanceSetMesh(Actor.InstanceRid, _mesh);
    }
    
    public override void Update(GameTime gameTime)
    {
        _elapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.Up, _elapsed);
        _rendering.InstanceSetRotation(Actor.InstanceRid, rotation);
    }
}