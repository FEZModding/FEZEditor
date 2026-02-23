using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpGLTF.Schema2;
using PrimitiveType = Microsoft.Xna.Framework.Graphics.PrimitiveType;

namespace FezEditor.Actors;

public class BackgroundPlaneSprite : ActorComponent
{
    public Vector3 PlaneSize { get; private set; }

    public bool Animated { get; private set; }

    public bool Billboard { get; set; } = false;

    public Color Color { get; set; } = Color.White;

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _material;

    private readonly Rid _camera;

    private readonly Transform _transform;

    private List<FrameContent> _frames = [];
    
    private Effect? _animatedEffect;

    private Effect? _staticEffect;
    
    private Texture2D? _texture;

    private TimeSpan _frameElapsed = TimeSpan.Zero;

    private int _frameCounter;

    internal BackgroundPlaneSprite(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _camera = _rendering.WorldGetCamera(_rendering.InstanceGetWorld(actor.InstanceRid));
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
        _transform = actor.GetComponent<Transform>();
    }

    public override void LoadContent(IContentManager content)
    {
        _animatedEffect = content.Load<Effect>("Effects/AnimatedPlane");
        _staticEffect = content.Load<Effect>("Effects/StaticPlane");
    }

    public void Visualize(RAnimatedTexture animatedTexture)
    {
        _texture = RepackerExtensions.ConvertToTexture2D(animatedTexture);
        _rendering.MaterialAssignEffect(_material, _animatedEffect!);
        _rendering.MaterialAssignBaseTexture(_material, _texture);
        _frames = animatedTexture.Frames;
        PlaneSize = new Vector3(animatedTexture.FrameWidth / 16f, animatedTexture.FrameHeight / 16f, 0.125f);
        VisualizeInternal();
    }

    public void Visualize(RTexture2D texture)
    {
        _texture = RepackerExtensions.ConvertToTexture2D(texture);
        _rendering.MaterialAssignEffect(_material, _staticEffect!);
        _rendering.MaterialAssignBaseTexture(_material, _texture);
        _frames = new List<FrameContent>();
        PlaneSize = new Vector3(texture.Width / 16f, texture.Height / 16f, 0.125f);
        VisualizeInternal();
    }

    private void VisualizeInternal()
    {
        var surface = MeshSurface.CreateQuad(PlaneSize);
        _rendering.MeshClear(_mesh);
        
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
        _rendering.MaterialSetBlendMode(_material, BlendMode.AlphaBlend);
        _rendering.MaterialSetCullMode(_material, CullMode.CullCounterClockwiseFace);
        
        _frameCounter = 0;
        Animated = _frames.Count > 0;
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
    }

    public override void Update(GameTime gameTime)
    {
        if (Animated)
        {
            var currentFrame = _frames[_frameCounter];
            if (_frameElapsed < currentFrame.Duration)
            {
                _frameElapsed += gameTime.ElapsedGameTime;
            }
            else
            {
                var textureSize = new Vector2(_texture!.Width, _texture!.Height);
                var transform = Mathz.CreateTextureTransform(currentFrame.Rectangle.ToXna(), textureSize);
                _rendering.MaterialSetTextureTransform(_material, transform);
                _frameCounter = Mathz.Clamp(_frameCounter + 1, 0, _frames.Count - 1);
                _frameElapsed = TimeSpan.Zero;
            }
        }

        _rendering.MaterialSetAlbedo(_material, Color);

        var rotation = _transform.Rotation;
        if (Billboard && _camera.IsValid)
        {
            var viewMatrix = _rendering.CameraGetView(_camera);
            var invViewMatrix = Matrix.Invert(viewMatrix);
            var translation = invViewMatrix.Translation;

            var toCamera = (translation - _transform.Position) * new Vector3(1, 0, 1);
            var angleY = 0f;
            if (toCamera.LengthSquared() > 0.0001f)
            {
                toCamera.Normalize();
                angleY = (float)Math.Atan2(toCamera.X, toCamera.Z);
            }

            rotation = Quaternion.CreateFromAxisAngle(Vector3.Up, angleY);
        }

        _transform.Rotation = rotation;
    }
}