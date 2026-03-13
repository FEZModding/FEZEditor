using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Sky;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class SkyLayersMesh : SkyBaseMesh
{
    private static readonly SamplerState PointUWrapVClamp = new()
    {
        Filter = TextureFilter.Point,
        AddressU = TextureAddressMode.Wrap,
        AddressV = TextureAddressMode.Clamp
    };

    private const float MovementSpeed = 0.025f;

    internal IReadOnlyDictionary<string, Texture2D> Textures => _textures;

    private readonly Dictionary<FaceOrientation, List<LayerInstance>> _sides = new();

    private readonly Dictionary<string, Texture2D> _textures = new();

    private Effect _effect = null!;

    internal SkyLayersMesh(Game game, Actor actor) : base(game, actor)
    {
        foreach (var face in Enum.GetValues<FaceOrientation>().Where(fo => fo.IsSide()))
        {
            _sides[face] = new List<LayerInstance>();
        }
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var layer in _sides.Values.SelectMany(l => l))
        {
            _rendering.FreeRid(layer.Material);
            _rendering.FreeRid(layer.Mesh);
            _rendering.FreeRid(layer.Instance);
        }

        foreach (var texture in _textures.Values)
        {
            texture.Dispose();
        }

        _sides.Clear();
        _textures.Clear();
    }

    public override void LoadContent(IContentManager content)
    {
        _effect = content.Load<Effect>("Effects/SkyLayersMesh");
    }

    public void Visualize(string sky, List<SkyLayer> layers)
    {
        for (var index = 0; index < layers.Count; index++)
        {
            var layer = layers[index];
            if (!_textures.TryGetValue(layer.Name, out var layerTexture))
            {
                var texture = (RTexture2D)_resources.Load($"Skies/{sky}/{layer.Name}");
                layerTexture = RepackerExtensions.ConvertToTexture2D(texture);
                _textures[layer.Name] = layerTexture;
            }

            Texture2D? obsTexture = null;
            if (layer.Name == "OBS_SKY_A")
            {
                var texture = (RTexture2D)_resources.Load($"Skies/{sky}/OBS_SKY_C");
                obsTexture = RepackerExtensions.ConvertToTexture2D(texture);
                _textures[layer.Name] = obsTexture;
            }

            var side = 0;
            foreach (var (face, instancesList) in _sides)
            {
                var texture = (obsTexture != null && face != FaceOrientation.Left) ? obsTexture : layerTexture;
                var material = _rendering.MaterialCreate();
                var compareFunction = layer.InFront ? CompareFunction.Always : CompareFunction.LessEqual;

                _rendering.MaterialAssignEffect(material, _effect);
                _rendering.MaterialAssignBaseTexture(material, texture);
                _rendering.MaterialSetDepthWrite(material, false);
                _rendering.MaterialSetDepthTest(material, compareFunction);
                _rendering.MaterialSetBlendMode(material, BlendMode.NonPremultiplied);
                _rendering.MaterialSetCullMode(material, CullMode.None);
                _rendering.MaterialSetStencilWrite(material, false);

                var surface = MeshSurface.CreateFaceQuad(Vector3.One, -face.AsVector() / 2f, face);
                var mesh = _rendering.MeshCreate();
                _rendering.MeshAddSurface(mesh, PrimitiveType.TriangleList, surface, material);

                var instance = _rendering.InstanceCreate(Actor.InstanceRid);
                _rendering.InstanceSetMesh(instance, mesh);

                instancesList.Add(new LayerInstance
                {
                    Instance = instance,
                    Mesh = mesh,
                    Material = material,
                    Index = index,
                    Side = side++,
                    Opacity = layer.Opacity,
                    FogTint = layer.FogTint,
                    TexCoords = new Vector2(texture.Width, texture.Height) * Mathz.TrixelSize,
                    WindOffset = 0f
                });
            }
        }
    }

    public override void Update(GameTime gameTime)
    {
        var orthogonal = Sky.Camera.Projection == Camera.ProjectionType.Orthographic;
        var camPos = Sky.Camera.Position;

        Vector3 scale;
        if (orthogonal)
        {
            scale = BaseScale * (Sky.Camera.Size * 2f);
            Actor.Transform.Scale = scale;
            Actor.Transform.Position = camPos;
        }
        else
        {
            scale = Sky.LevelSize + new Vector3(ExtraScale);
            Actor.Transform.Scale = scale;
            Actor.Transform.Position = Sky.LevelSize / 2f;
        }

        var sideOffset = Vector3.Dot(camPos - Sky.LevelSize / 2f, Sky.Camera.InverseView.Right);
        var heightOffset = camPos.Y - Sky.LevelSize.Y / 2f - Sky.ViewOffset;
        var fogColor = Sky.FogColor;
        var cloudTint = Sky.CloudTint;

        foreach (var layers in _sides.Values)
        {
            foreach (var layer in layers)
            {
                var samplerState = Sky.VerticalTiling ? SamplerState.PointWrap : PointUWrapVClamp;
                _rendering.MaterialSetSamplerState(layer.Material, samplerState);

                if (Sky.HorizontalScrolling)
                {
                    var delta = (float)gameTime.ElapsedGameTime.TotalSeconds * (Sky.Clock.TimeFactor * TimeScaleFactor);
                    layer.WindOffset += delta * Sky.WindSpeed * MovementSpeed;
                }

                #region Layer Texture Matrix

                {
                    var layerDepth = layer.Index / ((layers.Count > 1) ? (layers.Count - 1) : 1);
                    var uv = new Vector2(scale.X, scale.Y) / layer.TexCoords;
                    var tc = new Vector2(sideOffset, heightOffset) / layer.TexCoords;

                    // U with per-face base offset [0, 0.25, 0.5, 0.75]
                    var u = (Sky.NoPerFaceLayerXOffset ? 0 : layer.Side / 4f)
                            + Sky.LayerBaseXOffset
                            + tc.X * Sky.HorizontalDistance
                            + tc.X * Sky.InterLayerHorizontalDistance * layerDepth
                            - layer.WindOffset * Sky.WindDistance
                            - layer.WindOffset * Sky.WindParallax * layerDepth;

                    // V (layerDepth adjusted for VerticalTiling)
                    var layerDepthV = Sky.VerticalTiling ? layerDepth : layerDepth - 0.5f;
                    var v = Sky.LayerBaseHeight
                            + layerDepthV * Sky.LayerBaseSpacing
                            - tc.Y * Sky.VerticalDistance
                            - layerDepthV * Sky.InterLayerVerticalDistance * tc.Y;

                    var textureMatrix = new Matrix(
                        -uv.X, 0, 0, 0,
                        0, uv.Y, 0, 0,
                        -u + uv.X / 2, v - uv.Y / 2, 1, 0,
                        0, 0, 0, 1
                    );

                    _rendering.MaterialSetTextureTransform(layer.Material, textureMatrix);
                }

                #endregion

                #region Diffuse Tint

                {
                    var diffuse = Vector3.Lerp(cloudTint.ToVector3(), fogColor.ToVector3(), layer.FogTint);
                    var albedo = new Color(diffuse.X, diffuse.Y, diffuse.Z, layer.Opacity);
                    _rendering.MaterialSetAlbedo(layer.Material, albedo);
                }

                #endregion
            }
        }
    }

    private record LayerInstance
    {
        public Rid Instance { get; init; }

        public Rid Mesh { get; init; }

        public Rid Material { get; init; }

        public int Index { get; init; }

        public int Side { get; init; }

        public float Opacity { get; init; }

        public float FogTint { get; init; }

        public Vector2 TexCoords { get; init; }

        public float WindOffset { get; set; }
    }
}