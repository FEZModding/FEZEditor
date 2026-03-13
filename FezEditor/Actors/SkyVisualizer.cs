using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Sky;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class SkyVisualizer : ActorComponent
{
    public Camera Camera { get; private set; } = null!;

    public Clock Clock { get; private set; } = null!;

    public Color FogColor { get; private set; } = Color.Black;

    public Color CloudTint { get; private set; } = Color.White;

    public float AmbientFactor { get; private set; } = 0.1f;

    public bool VerticalTiling { get; private set; }

    public bool HorizontalScrolling { get; private set; }

    public float LayerBaseHeight { get; private set; }

    public float InterLayerVerticalDistance { get; private set; }

    public float InterLayerHorizontalDistance { get; private set; }

    public float HorizontalDistance { get; private set; }

    public float VerticalDistance { get; private set; }

    public float LayerBaseSpacing { get; private set; }

    public float WindSpeed { get; private set; }

    public float WindParallax { get; private set; }

    public float WindDistance { get; private set; }

    public float CloudsParallax { get; private set; }

    public float ShadowOpacity { get; private set; }

    public bool FoliageShadows { get; private set; }

    public bool NoPerFaceLayerXOffset { get; private set; }

    public float LayerBaseXOffset { get; private set; }

    public Vector3 LevelSize { get; set; } = new(16f);

    public float ViewOffset { get; set; }

    public bool Rainy { get; set; }

    public bool Shadows
    {
        get => _shadowsEnabled;
        set
        {
            _shadowsEnabled = value;
            if (_shadows != null)
            {
                _shadows.Visible = value;
            }
        }
    }

    private readonly ResourceService _resources;

    private Scene _scene = null!;

    private Actor? _clouds;

    private Actor? _background;

    private Actor? _stars;

    private Actor? _layers;

    private Actor? _shadows;

    private Color[] _fogColors = new[] { Color.Black };

    private Color[] _cloudTintColors = new[] { Color.White };

    private Texture2D? _cloudTintTexture;

    private string _backgroundName = string.Empty;

    private string _starsName = string.Empty;

    private string _shadowsName = string.Empty;

    private string _cloudTintName = string.Empty;

    private bool _shadowsEnabled;

    internal SkyVisualizer(Game game, Actor actor) : base(game, actor)
    {
        _resources = game.GetService<ResourceService>();
    }

    public void Initialize(Scene scene, Camera camera, Clock clock)
    {
        _scene = scene;
        Camera = camera;
        Clock = clock;
    }

    public void Visualize(Sky sky)
    {
        DestroyActors();

        _backgroundName = sky.Background;
        _starsName = sky.Stars;
        _shadowsName = sky.Shadows;
        _cloudTintName = sky.CloudTint;

        #region Sky properties

        // Sky properties are exposed here so child mesh components can read them
        // without depending on the Sky data object directly.

        VerticalTiling = sky.VerticalTiling;
        HorizontalScrolling = sky.HorizontalScrolling;
        LayerBaseHeight = sky.LayerBaseHeight;
        InterLayerVerticalDistance = sky.InterLayerVerticalDistance;
        InterLayerHorizontalDistance = sky.InterLayerHorizontalDistance;
        HorizontalDistance = sky.HorizontalDistance;
        VerticalDistance = sky.VerticalDistance;
        LayerBaseSpacing = sky.LayerBaseSpacing;
        WindSpeed = sky.WindSpeed;
        WindParallax = sky.WindParallax;
        WindDistance = sky.WindDistance;
        CloudsParallax = sky.CloudsParallax;
        ShadowOpacity = sky.ShadowOpacity;
        FoliageShadows = sky.FoliageShadows;
        NoPerFaceLayerXOffset = sky.NoPerFaceLayerXOffset;
        LayerBaseXOffset = sky.LayerBaseXOffset;

        #endregion

        #region Fog colors

        if (!string.IsNullOrEmpty(sky.Background))
        {
            var bgTexture = (RTexture2D)_resources.Load($"Skies/{sky.Name}/{sky.Background}");
            _fogColors = new Color[bgTexture.Width];
            for (var x = 0; x < bgTexture.Width; x++)
            {
                var offset = (bgTexture.Height / 2 * bgTexture.Width + x) * 4;
                _fogColors[x] = new Color(
                    bgTexture.TextureData[offset],
                    bgTexture.TextureData[offset + 1],
                    bgTexture.TextureData[offset + 2],
                    bgTexture.TextureData[offset + 3]
                );
            }
        }

        #endregion

        #region Cloud tint

        _cloudTintTexture?.Dispose();
        _cloudTintTexture = null;

        if (!string.IsNullOrEmpty(sky.CloudTint))
        {
            var tintTexture = (RTexture2D)_resources.Load($"Skies/{sky.Name}/{sky.CloudTint}");
            _cloudTintTexture = RepackerExtensions.ConvertToTexture2D(tintTexture);
            _cloudTintColors = new Color[tintTexture.Width];
            for (var x = 0; x < tintTexture.Width; x++)
            {
                var offset = (tintTexture.Height / 2 * tintTexture.Width + x) * 4;
                _cloudTintColors[x] = new Color(
                    tintTexture.TextureData[offset],
                    tintTexture.TextureData[offset + 1],
                    tintTexture.TextureData[offset + 2],
                    tintTexture.TextureData[offset + 3]
                );
            }
        }

        #endregion

        #region Clouds

        {
            _clouds = _scene.CreateActor();
            var mesh = _clouds.AddComponent<SkyCloudsMesh>();
            mesh.Sky = this;
            mesh.Visualize(sky.Name, sky.Clouds, sky.Density);
        }

        #endregion

        #region Background

        {
            _background = _scene.CreateActor();
            var mesh = _background.AddComponent<SkyBackgroundMesh>();
            mesh.Sky = this;
            mesh.Visualize(sky.Name, sky.Background);
        }

        #endregion

        #region Stars

        {
            _stars = _scene.CreateActor();
            var mesh = _stars.AddComponent<SkyStarsMesh>();
            mesh.Sky = this;
            mesh.Visualize(sky.Name, sky.Stars, Rainy);
        }

        #endregion

        #region Background Layers

        {
            _layers = _scene.CreateActor();
            var mesh = _layers.AddComponent<SkyLayersMesh>();
            mesh.Sky = this;
            mesh.Visualize(sky.Name, sky.Layers);
        }

        #endregion
    }

    public void VisualizeShadows(string skyName, string shadows)
    {
        _shadowsName = shadows;
        if (_shadows != null)
        {
            _scene.DestroyActor(_shadows);
        }

        if (string.IsNullOrEmpty(shadows))
        {
            _shadows = null;
            return;
        }

        _shadows = _scene.CreateActor();
        _shadows.Visible = Shadows;
        var mesh = _shadows.AddComponent<SkyShadowsMesh>();
        mesh.Sky = this;
        mesh.Visualize(skyName, shadows);
    }

    public override void Update(GameTime gameTime)
    {
        FogColor = ComputeTint(_fogColors);
        CloudTint = ComputeTint(_cloudTintColors);
        AmbientFactor = MathF.Max(Vector3.Dot(FogColor.ToVector3(), new Vector3(1f / 3f)), 0.1f);
    }

    public Texture2D? GetPreviewTexture(string textureName)
    {
        if (string.IsNullOrEmpty(textureName))
        {
            return null;
        }

        if (_cloudTintTexture is { IsDisposed: false } &&
            string.Equals(textureName, _cloudTintName, StringComparison.OrdinalIgnoreCase))
        {
            return _cloudTintTexture;
        }

        if (_background?.TryGetComponent<SkyBackgroundMesh>(out var bgMesh) == true &&
            bgMesh!.Texture is { IsDisposed: false } &&
            string.Equals(textureName, _backgroundName, StringComparison.OrdinalIgnoreCase))
        {
            return bgMesh.Texture;
        }

        if (_stars?.TryGetComponent<SkyStarsMesh>(out var starsMesh) == true &&
            starsMesh!.Texture is { IsDisposed: false } &&
            string.Equals(textureName, _starsName, StringComparison.OrdinalIgnoreCase))
        {
            return starsMesh.Texture;
        }

        if (_shadows?.TryGetComponent<SkyShadowsMesh>(out var shadowsMesh) == true &&
            shadowsMesh!.Texture is { IsDisposed: false } &&
            string.Equals(textureName, _shadowsName, StringComparison.OrdinalIgnoreCase))
        {
            return shadowsMesh.Texture;
        }

        if (_clouds?.TryGetComponent<SkyCloudsMesh>(out var cloudsMesh) == true &&
            cloudsMesh!.Textures.TryGetValue(textureName, out var cloudTex) &&
            !cloudTex.IsDisposed)
        {
            return cloudTex;
        }

        if (_layers?.TryGetComponent<SkyLayersMesh>(out var layersMesh) == true &&
            layersMesh!.Textures.TryGetValue(textureName, out var layerTex) &&
            !layerTex.IsDisposed)
        {
            return layerTex;
        }

        return null;
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _cloudTintTexture?.Dispose();
        DestroyActors();
    }

    private void DestroyActors()
    {
        if (_clouds != null)
        {
            _scene.DestroyActor(_clouds);
            _clouds = null;
        }

        if (_background != null)
        {
            _scene.DestroyActor(_background);
            _background = null;
        }

        if (_stars != null)
        {
            _scene.DestroyActor(_stars);
            _stars = null;
        }

        if (_layers != null)
        {
            _scene.DestroyActor(_layers);
            _layers = null;
        }

        if (_shadows != null)
        {
            _scene.DestroyActor(_shadows);
            _shadows = null;
        }
    }

    private Color ComputeTint(Color[] colors)
    {
        var index = Clock.DayFraction * colors.Length;
        if (Mathz.IsEqualApprox(index, colors.Length))
        {
            index = 0f;
        }

        var color1 = colors[Math.Max((int)Math.Floor(index), 0)];
        var color2 = colors[Math.Min((int)Math.Ceiling(index), colors.Length - 1)];

        return Color.Lerp(color1, color2, Mathz.Frac(index));
    }
}