using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class SkyShadowsMesh : SkyBaseMesh
{
    private const float TileSize = 32f;

    internal Texture2D? Texture { get; private set; }

    private readonly Rid _instance;

    private readonly Rid _mesh;

    private readonly Rid _material;

    private float _sineAccumulator;

    private float _sineSpeed;

    private float _scrollOffset;

    private readonly Random _random = new();

    internal SkyShadowsMesh(Game game, Actor actor) : base(game, actor)
    {
        _instance = _rendering.InstanceCreate(Actor.InstanceRid);
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _rendering.InstanceSetMesh(_instance, _mesh);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_instance.IsValid)
        {
            _rendering.FreeRid(_material);
            _rendering.FreeRid(_mesh);
            _rendering.FreeRid(_instance);
        }

        Texture?.Dispose();
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/SkyShadowsMesh");
        _rendering.MaterialAssignEffect(_material, effect);
        _rendering.MaterialSetDepthWrite(_material, false);
        _rendering.MaterialSetDepthTest(_material, CompareFunction.Always);
        _rendering.MaterialSetStencilTest(_material, CompareFunction.Equal, 1);
        _rendering.MaterialSetCullMode(_material, CullMode.None);
        _rendering.MaterialSetSamplerState(_material, SamplerState.LinearWrap);
    }

    public void Visualize(string skyName, string shadows)
    {
        Texture?.Dispose();
        Texture = null;

        if (string.IsNullOrEmpty(shadows))
        {
            Actor.Visible = false;
            return;
        }

        var rTexture = (RTexture2D)_resources.Load($"Skies/{skyName}/{shadows}");
        Texture = RepackerExtensions.ConvertToTexture2D(rTexture);
        _rendering.MaterialAssignBaseTexture(_material, Texture);

        Actor.Visible = true;
        _sineAccumulator = 0f;
        _sineSpeed = 0f;
        _scrollOffset = 0f;

        var surface = MeshSurface.CreateQuad(new Vector3(2f));
        _rendering.MeshClear(_mesh);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
    }

    public override void Update(GameTime gameTime)
    {
        if (Texture == null)
        {
            return;
        }

        var elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var levelScale = Sky.LevelSize.X / TileSize;
        var opacity = Sky.ShadowOpacity;

        _rendering.MaterialShaderSetParam(_material, "Canopy", Sky.FoliageShadows);
        if (Sky.FoliageShadows)
        {
            _sineSpeed = MathHelper.Lerp(_sineSpeed, _random.Between(0f, elapsed), 0.1f);
            _sineAccumulator += _sineSpeed;

            var s = MathF.Sin(_sineAccumulator);
            var textureMatrix = new Matrix(
                levelScale, 0, 0, 0,
                0, levelScale, 0, 0,
                s / 100f, s / 100f, 1, 0,
                0, 0, 0, 1
            );

            _rendering.MaterialSetBlendMode(_material, BlendMode.Minimum);
            _rendering.MaterialSetTextureTransform(_material, textureMatrix);
        }
        else
        {
            opacity *= Sky.AmbientFactor;

            _scrollOffset += elapsed * -0.01f * Sky.Clock.TimeFactor * TimeScaleFactor * Sky.WindSpeed;
            var textureMatrix = new Matrix(
                levelScale, 0, 0, 0,
                0, levelScale, 0, 0,
                _scrollOffset, 0, 1, 0,
                0, 0, 0, 1
            );

            _rendering.MaterialSetBlendMode(_material, BlendMode.Multiply);
            _rendering.MaterialSetTextureTransform(_material, textureMatrix);
        }

        _rendering.MaterialSetAlbedo(_material, new Color(1f, 1f, 1f, opacity));
        _rendering.InstanceSetVisibility(_instance, opacity > 0.001f);
    }
}
