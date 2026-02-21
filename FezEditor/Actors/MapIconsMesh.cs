using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.MapTree;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class MapIconsMesh : ActorComponent
{
    private const int MapIconInstances = 7;

    private const float FrameWidth = 1f / MapIconInstances;

    private const float MapIconScale = 0.33f;

    private readonly RenderingService _rendering;

    private readonly Transform _transform;

    private readonly Rid _multiMesh;

    private readonly Rid _mesh;

    private readonly Rid _material;

    private readonly Rid _camera;

    public MapIconsMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _transform = actor.GetComponent<Transform>();
        _material = _rendering.MaterialCreate();
        _mesh = _rendering.MeshCreate();
        _multiMesh = _rendering.MultiMeshCreate();
        _camera = _rendering.WorldGetCamera(_rendering.InstanceGetWorld(actor.InstanceRid));
        _rendering.MultiMeshSetMesh(_multiMesh, _mesh);
        _rendering.MultiMeshAllocate(_multiMesh, MapIconInstances, MultiMeshDataType.Matrix);
        _rendering.InstanceSetMultiMesh(actor.InstanceRid, _multiMesh);
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/MapIconsMesh");
        var texture = content.Load<Texture2D>("MapIcons");
        var surface = MeshSurface.CreateQuad(Vector3.One);
        
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
        _rendering.MaterialAssignEffect(_material, effect);
        _rendering.MaterialAssignBaseTexture(_material, texture);
        _rendering.MaterialSetFillMode(_material, FillMode.Solid);
        _rendering.MaterialSetCullMode(_material, CullMode.CullClockwiseFace);
        _rendering.MaterialSetDepthWrite(_material, false);
        _rendering.MaterialSetDepthTest(_material, CompareFunction.Always);
    }

    public void Visualize(MapNode node)
    {
        bool[] conditions =
        [
            node.HasWarpGate,
            node.HasLesserGate,
            node.Conditions.ChestCount > 0,
            node.Conditions.LockedDoorCount > 0,
            node.Conditions.CubeShardCount > 0,
            node.Conditions.SplitUpCount > 0,
            node.Conditions.SecretCount > 0
        ];

        var y = 0;
        for (var i = 0; i < MapIconInstances; i++)
        {
            if (!conditions[i])
            {
                _rendering.MultiMeshSetInstanceMatrix(_multiMesh, i, new Matrix());
                continue;
            }

            var position = Vector3.Down * y++ * MapIconScale;
            var scale = new Vector3(MapIconScale);
            var tcOffset = new Vector2(i * FrameWidth, 0f);
            var tcScale = new Vector2(FrameWidth, 1f);

            _rendering.MultiMeshSetInstanceMatrix(_multiMesh, i, new Matrix(
                position.X, position.Y, position.Z, 0f,
                scale.X, scale.Y, scale.Z, 0f,
                tcOffset.X, tcOffset.Y, tcScale.X, tcScale.Y,
                0f, 0f, 0f, 0f
            ));
        }

        _transform.Position = node.NodeType.GetSizeFactor() / 2f * new Vector3(1f, 1f, -1f) +
                              MapIconScale * new Vector3(1f, 0f, -1f);
    }

    public override void Update(GameTime gameTime)
    {
        var viewMatrix = _rendering.CameraGetView(_camera);
        _rendering.MaterialShaderSetParam(_material, "CameraRotation", Matrix.Invert(viewMatrix));
        _rendering.MaterialShaderSetParam(_material, "Billboard", 1f);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_multiMesh);
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
    }
}