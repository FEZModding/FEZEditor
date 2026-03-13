using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class SkyCloudsMesh : SkyBaseMesh
{
    private const int BaseCloudCount = 64;

    private const float HeightRange = 192f;

    private const float HalfHeightRange = HeightRange / 2f;

    private const float OrthoHeightSpread = 0.2f;

    private const float OrthoWrapHalf = HalfHeightRange * OrthoHeightSpread;

    private const float OrthoWrapFull = OrthoWrapHalf * 2f;

    private const float OrthoOrbitPadding = 32f / 2.5f;

    private const float PerspectiveOrbitBase = 32f;

    private const float PerspectiveOrbitDistanceScale = 48f;

    private const float PerspectiveScaleBase = 4f;

    private const float ParallaxHorizontalFactor = 2.25f;

    private const float ParallaxLevelPadding = 32f;

    private const float WindSpeedFactor = 0.025f;

    private const float VisibilityFadeSpeed = 5f;

    private const float MinVisibleOpacity = 1f / 510f;

    internal IReadOnlyDictionary<string, Texture2D> Textures => _textures;

    private readonly List<CloudInstance> _clouds = new();

    private readonly Dictionary<string, Texture2D> _textures = new();

    private readonly Random _random = new();

    private Effect _effect = null!;

    private Vector3 _lastCamPos;

    internal SkyCloudsMesh(Game game, Actor actor) : base(game, actor)
    {
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var cloud in _clouds)
        {
            _rendering.FreeRid(cloud.Material);
            _rendering.FreeRid(cloud.Mesh);
            _rendering.FreeRid(cloud.Instance);
        }

        foreach (var texture in _textures.Values)
        {
            texture.Dispose();
        }

        _clouds.Clear();
        _textures.Clear();
    }

    public override void LoadContent(IContentManager content)
    {
        _effect = content.Load<Effect>("Effects/SkyCloudsMesh");
    }

    public void Visualize(string sky, List<string> clouds, float density)
    {
        if (clouds.Count == 0)
        {
            return;
        }

        var textures = new List<Texture2D>();
        foreach (var cloud in clouds)
        {
            if (!_textures.TryGetValue(cloud, out var tex))
            {
                var rTexture = (RTexture2D)_resources.Load($"Skies/{sky}/{cloud}");
                tex = RepackerExtensions.ConvertToTexture2D(rTexture);
                _textures[cloud] = tex;
            }

            textures.Add(tex);
        }

        var total = (int)(BaseCloudCount * density);
        var rows = (int)Math.Sqrt(total);
        if (rows == 0)
        {
            return;
        }

        var perRow = (float)total / rows;
        var phiSeed = _random.Between(0f, MathHelper.TwoPi);
        var heightSeed = _random.Between(0f, HeightRange);

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < perRow; col++)
            {
                var texture = textures[_random.Next(textures.Count)];
                var layer = CloudLayer.Choose(_random);

                var instance = _rendering.InstanceCreate(Actor.InstanceRid);
                var mesh = _rendering.MeshCreate();
                var material = _rendering.MaterialCreate();

                _rendering.MaterialAssignEffect(material, _effect);
                _rendering.MaterialAssignBaseTexture(material, texture);
                _rendering.MaterialSetBlendMode(material, BlendMode.Maximum);
                _rendering.MaterialSetDepthWrite(material, false);
                _rendering.MaterialSetDepthTest(material, CompareFunction.LessEqual);
                _rendering.MaterialSetCullMode(material, CullMode.None);
                _rendering.MaterialSetSamplerState(material, SamplerState.PointClamp);
                _rendering.MaterialSetStencilWrite(material, false);

                var surface = MeshSurface.CreateFaceQuad(Vector3.One, FaceOrientation.Front);
                _rendering.MeshAddSurface(mesh, PrimitiveType.TriangleList, surface, material);
                _rendering.InstanceSetMesh(instance, mesh);

                var phiJitter = _random.Between(0f, 1f / rows * MathHelper.TwoPi);
                var phi = ((float)row / rows * MathHelper.TwoPi + phiSeed + phiJitter) % MathHelper.TwoPi;

                var heightJitter = _random.Between(0f, 1f / perRow * HeightRange);
                var localHeight = ((col / perRow * HeightRange + heightSeed + heightJitter) % HeightRange) -
                                  HalfHeightRange;

                _clouds.Add(new CloudInstance
                {
                    Instance = instance,
                    Mesh = mesh,
                    Material = material,
                    TextureWidth = texture.Width,
                    TextureHeight = texture.Height,
                    Layer = layer,
                    Phi = phi,
                    LocalHeightOffset = localHeight,
                    GlobalHeightOffset = 0f,
                    VisibilityFactor = 0f,
                    ActualVisibility = false
                });
            }
        }
    }

    public override void Update(GameTime gameTime)
    {
        var elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var delta = elapsed * Sky.Clock.TimeFactor * TimeScaleFactor;
        var orthogonal = Sky.Camera.Projection == Camera.ProjectionType.Orthographic;

        var levelHalf = Sky.LevelSize / 2f;
        var camPos = Sky.Camera.Position;
        var camRight = Sky.Camera.InverseView.Right;
        var camForward = Sky.Camera.InverseView.Forward;
        if (camForward.Z != 0f)
        {
            camForward.Z *= -1f;
        }

        var levelWidth = Math.Abs(Vector3.Dot(Sky.LevelSize, camRight)) / ParallaxLevelPadding;
        var cameraDeltaX = Vector3.Dot(camRight, camPos) - Vector3.Dot(camRight, _lastCamPos);
        var cameraDeltaY = camPos.Y - _lastCamPos.Y;
        var parallaxLevelWidth = Math.Abs(Vector3.Dot(Sky.LevelSize + Vector3.One * ParallaxLevelPadding, camRight));

        Actor.Transform.Position = camPos;

        foreach (var cloud in _clouds)
        {
            #region Movement

            if (orthogonal)
            {
                // Height wrapping
                while (cloud.GetHeight(OrthoHeightSpread) - camPos.Y > OrthoWrapHalf)
                {
                    cloud.GlobalHeightOffset -= OrthoWrapFull;
                }

                while (cloud.GetHeight(OrthoHeightSpread) - camPos.Y < -OrthoWrapHalf)
                {
                    cloud.GlobalHeightOffset += OrthoWrapFull;
                }

                // Parallax
                var parallaxStrength = MathHelper.Lerp(1f, cloud.Layer.ParallaxFactor, Sky.CloudsParallax);
                cloud.Phi -= parallaxStrength * ParallaxHorizontalFactor * cameraDeltaX / parallaxLevelWidth;
                cloud.GlobalHeightOffset += parallaxStrength * cameraDeltaY;

                // Wind
                var dPhi = delta * Sky.WindSpeed * WindSpeedFactor * cloud.Layer.SpeedFactor / levelWidth;
                cloud.Phi -= dPhi;
            }
            else
            {
                cloud.GlobalHeightOffset = levelHalf.Y;
            }

            #endregion

            #region World Position

            var orbitRadius = orthogonal
                ? levelHalf + Vector3.One * OrthoOrbitPadding
                : levelHalf + Vector3.One *
                (PerspectiveOrbitBase + PerspectiveOrbitDistanceScale * cloud.Layer.DistanceFactor);

            var spreadFactor = orthogonal ? OrthoHeightSpread : 1f;
            var cloudWorldPos = new Vector3(
                MathF.Sin(cloud.Phi) * orbitRadius.X + levelHalf.X,
                cloud.GetHeight(spreadFactor),
                MathF.Cos(cloud.Phi) * orbitRadius.Z + levelHalf.Z
            );

            #endregion

            #region Visibility

            if (orthogonal)
            {
                var wasVisible = cloud.ActualVisibility;
                cloud.ActualVisibility = Vector3.Dot(cloudWorldPos - levelHalf, camForward) <= 0f;

                if (!wasVisible && cloud.ActualVisibility)
                {
                    cloud.VisibilityFactor = 0f;
                }
            }
            else
            {
                cloud.ActualVisibility = true;
            }

            cloud.VisibilityFactor = MathHelper.Clamp(
                cloud.VisibilityFactor + elapsed * VisibilityFadeSpeed * (cloud.ActualVisibility ? 1f : -1f),
                0f, 1f);

            var layerOpacity = cloud.Layer.Opacity * Sky.AmbientFactor * MathF.Pow(cloud.VisibilityFactor, 2);
            var enabled = layerOpacity > MinVisibleOpacity;
            _rendering.MaterialShaderSetParam(cloud.Material, "Opacity", layerOpacity);
            _rendering.InstanceSetVisibility(cloud.Instance, enabled);

            if (!enabled)
            {
                continue;
            }

            #endregion

            #region Transform

            var scale = new Vector3(cloud.TextureWidth, cloud.TextureHeight, 1f) * Mathz.TrixelSize;
            if (!orthogonal)
            {
                scale *= PerspectiveScaleBase + cloud.Layer.DistanceFactor * 2f;
            }

            var rotation = orthogonal
                ? Quaternion.CreateFromRotationMatrix(Sky.Camera.InverseView)
                : Quaternion.CreateFromYawPitchRoll(cloud.Phi, MathF.PI, MathF.PI);

            _rendering.InstanceSetPosition(cloud.Instance, cloudWorldPos - camPos);
            _rendering.InstanceSetRotation(cloud.Instance, rotation);
            _rendering.InstanceSetScale(cloud.Instance, scale);

            #endregion
        }

        _lastCamPos = camPos;
    }

    private class CloudInstance
    {
        public Rid Instance { get; init; }

        public Rid Mesh { get; init; }

        public Rid Material { get; init; }

        public int TextureWidth { get; init; }

        public int TextureHeight { get; init; }

        public CloudLayer Layer { get; init; }

        public float Phi { get; set; }

        public float LocalHeightOffset { get; init; }

        public float GlobalHeightOffset { get; set; }

        public float VisibilityFactor { get; set; }

        public bool ActualVisibility { get; set; }

        public float GetHeight(float spreadFactor)
        {
            return LocalHeightOffset * spreadFactor + GlobalHeightOffset;
        }
    }

    private readonly record struct CloudLayer(
        float SpeedFactor,
        float DistanceFactor,
        float ParallaxFactor,
        float Opacity)
    {
        private static readonly CloudLayer Far = new(0.2f, 1f, 0.6f, 0.3f);

        private static readonly CloudLayer Middle = new(0.6f, 0.5f, 0.4f, 0.6f);

        private static readonly CloudLayer Near = new(1f, 0f, 0.2f, 1f);

        public static CloudLayer Choose(Random random)
        {
            return (random.Next() % 3) switch
            {
                0 => Far,
                1 => Middle,
                2 => Near,
                _ => throw new InvalidOperationException()
            };
        }
    }
}