using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PrimitiveType = Microsoft.Xna.Framework.Graphics.PrimitiveType;

namespace FezEditor.Actors;

public class LiquidMesh : ActorComponent
{
    private static readonly Dictionary<LiquidType, Color> SurfaceColor = new()
    {
        [LiquidType.Water] = new Color(61, 117, 254, 153),
        [LiquidType.Blood] = new Color(174, 26, 0, 153),
        [LiquidType.Sewer] = new Color(82, 127, 57, 153),
        [LiquidType.Lava] = new Color(209, 0, 0, 178),
        [LiquidType.Purple] = new Color(194, 1, 171, 153),
        [LiquidType.Green] = new Color(47, 255, 139, 153)
    };

    private static readonly Dictionary<LiquidType, Color> UnderwaterColor = new()
    {
        [LiquidType.Water] = new Color(40, 76, 162, 204),
        [LiquidType.Blood] = new Color(84, 0, 21, 204),
        [LiquidType.Sewer] = new Color(32, 70, 49, 204),
        [LiquidType.Lava] = new Color(150, 0, 0, 216),
        [LiquidType.Purple] = new Color(76, 9, 103, 204),
        [LiquidType.Green] = new Color(0, 167, 134, 204)
    };

    private readonly RenderingService _rendering;

    private readonly Transform _transform;

    private readonly Rid _instance;

    private readonly Rid _mesh;

    private readonly Rid _material;

    private readonly Rid _overlay;

    private readonly Rid _silhouette;

    private readonly Rid _camera;

    private readonly Rid _world;

    private LiquidType _type;

    private float _surfaceHeight;

    private BoundingBox _waterBox;

    public LiquidMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _instance = _rendering.InstanceCreate(Actor.InstanceRid);
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _overlay = _rendering.MaterialCreate();
        _silhouette = _rendering.MaterialCreate();
        _world = _rendering.InstanceGetWorld(actor.InstanceRid);
        _camera = _rendering.WorldGetCamera(_world);
        _rendering.InstanceSetMesh(_instance, _mesh);
        _transform = actor.GetComponent<Transform>();
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_rendering.WorldIsValid(_world))
        {
            _rendering.WorldSetFogType(_world, FogType.None);
        }
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
        _rendering.FreeRid(_overlay);
        _rendering.FreeRid(_silhouette);
        _rendering.FreeRid(_instance);
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/LiquidMesh");

        _rendering.MaterialAssignEffect(_material, effect);
        _rendering.MaterialSetCullMode(_material, CullMode.None);
        _rendering.MaterialSetDepthWrite(_material, false);
        _rendering.MaterialSetDepthTest(_material, CompareFunction.Less);

        _rendering.MaterialAssignEffect(_overlay, effect);
        _rendering.MaterialSetCullMode(_overlay, CullMode.None);
        _rendering.MaterialSetDepthWrite(_overlay, false);
        _rendering.MaterialSetDepthTest(_overlay, CompareFunction.Less);
        _rendering.MaterialSetStencilTest(_overlay, CompareFunction.Equal, 1);

        _rendering.MaterialAssignEffect(_silhouette, effect);
        _rendering.MaterialSetCullMode(_silhouette, CullMode.None);
        _rendering.MaterialSetDepthWrite(_silhouette, false);
        _rendering.MaterialSetDepthTest(_silhouette, CompareFunction.LessEqual);
        _rendering.MaterialSetBlendMode(_silhouette, BlendMode.Opaque);
        _rendering.MaterialSetStencilWrite(_silhouette, false);
    }

    public void Visualize(LiquidType type, float level, Vector3 bounds)
    {
        _type = type;
        _surfaceHeight = level - 0.5f;
        _waterBox = new BoundingBox(Vector3.Zero, new Vector3(bounds.X, _surfaceHeight, bounds.Z));

        _rendering.InstanceSetPosition(_instance, new Vector3(bounds.X / 2f, -1f, bounds.Z / 2f));
        _rendering.InstanceSetScale(_instance, new Vector3(bounds.X, level, bounds.Z));
        _transform.Position = Vector3.UnitY * (level + 0.5f);

        var surface = MeshSurface.CreateColoredBox(Vector3.One, Color.White);
        for (var i = 0; i < surface.Vertices.Length; i++)
        {
            surface.Vertices[i] += new Vector3(0f, -0.5f, 0f);
        }

        _rendering.MeshClear(_mesh);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _silhouette);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _overlay);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
    }

    public override void Update(GameTime gameTime)
    {
        var viewMatrix = _rendering.CameraGetView(_camera);
        var cameraPos = Matrix.Invert(viewMatrix).Translation;
        var underwater = _waterBox.Contains(cameraPos) != ContainmentType.Disjoint;

        var colorDict = underwater ? UnderwaterColor : SurfaceColor;
        var color = colorDict.GetValueOrDefault(_type, Color.White);
        _rendering.MaterialSetAlbedo(_material, color);

        var overlayColor = underwater
            ? SurfaceColor.GetValueOrDefault(_type, Color.White)
            : UnderwaterColor.GetValueOrDefault(_type, Color.White);
        _rendering.MaterialSetAlbedo(_overlay, overlayColor);

        var silhouetteColor = UnderwaterColor.GetValueOrDefault(_type, Color.White) with { A = 255 };
        _rendering.MaterialSetAlbedo(_silhouette, silhouetteColor);
        _rendering.MaterialSetColorWriteChannels(_silhouette, underwater
            ? ColorWriteChannels.None
            : ColorWriteChannels.All);

        if (!underwater)
        {
            _rendering.WorldSetFogType(_world, FogType.None);
            return;
        }

        _rendering.WorldSetFogType(_world, FogType.Exponential2);
        _rendering.WorldSetFogColor(_world, new Color(color.R / 2, color.G / 2, color.B / 2));
        _rendering.WorldSetFogDensity(_world, 0.05f);
    }
}