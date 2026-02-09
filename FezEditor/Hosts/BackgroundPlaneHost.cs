using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RAnimatedTexture = FEZRepacker.Core.Definitions.Game.Graphics.AnimatedTexture;
using RTexture2D = FEZRepacker.Core.Definitions.Game.XNA.Texture2D;

namespace FezEditor.Hosts;

public class BackgroundPlaneHost : Host
{
    public sealed override Rid Rid { get; protected set; }

    public Rid CameraRid { private get; set; }

    public Vector3 Position { get; set; } = Vector3.Zero;

    public Quaternion Rotation { get; set; } = Quaternion.Identity;

    public Vector3 Scale { get; set; } = Vector3.One;

    public Vector3 Size { get; set; } = Vector3.One / 16f;

    public BoundingBox Bounds { get; set; }

    public bool Animated { get; private set; }

    public bool Billboard { get; set; } = false;

    public bool DoubleSided { get; set; } = false;

    public Color Color { get; set; } = Color.White;

    private readonly List<FrameContent> _frames = new();

    private Vector2 _textureSize = Vector2.Zero;
    
    private TimeSpan _frameElapsed = TimeSpan.Zero;
    
    private int _frameCounter;

    private Rid _mesh;

    private Rid _material;

    public BackgroundPlaneHost(Game game) : base(game)
    {
        Rid = RenderingService.InstanceCreate(Rid.Invalid);
    }

    ~BackgroundPlaneHost()
    {
        Dispose();
    }

    public override void Load(object asset)
    {
        Effect effect;
        Texture2D baseTexture;
        List<FrameContent> frames;

        switch (asset)
        {
            case RAnimatedTexture animatedTexture:
            {
                frames = animatedTexture.Frames;
                effect = Game.Content.Load<Effect>("Effects/AnimatedPlane");
                baseTexture = animatedTexture.ToXna(RenderingService.GraphicsDevice);
                Size = new Vector3(animatedTexture.AtlasWidth / 16f, animatedTexture.AtlasHeight / 16f, 0.125f);
                break;
            }

            case RTexture2D texture:
            {
                frames = new List<FrameContent>();
                effect = Game.Content.Load<Effect>("StaticPlane");
                baseTexture = texture.ToXna(RenderingService.GraphicsDevice);
                Size = new Vector3(texture.Width / 16f, texture.Height / 16f, 0.125f);
                break;
            }

            default:
            {
                throw new NotSupportedException();
            }
        }

        _textureSize = new Vector2(baseTexture.Width, baseTexture.Height);
        _frameCounter = 0;
        _frames.AddRange(frames);
        Animated = _frames.Count > 0;

        _mesh = RenderingService.MeshCreate();
        RenderingService.InstanceSetMesh(Rid, _mesh);
        UpdateMeshSurface();

        _material = RenderingService.MaterialCreate(effect);
        RenderingService.MaterialSetBlendMode(_material, BlendMode.AlphaBlend);
        RenderingService.MaterialSetDepthWrite(_material, true);
        RenderingService.MaterialSetDepthTest(_material, CompareFunction.LessEqual);
        RenderingService.MaterialAssignBaseTexture(_material, baseTexture);
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
                var transform = Mathz.CreateTextureTransform(currentFrame.Rectangle.ToXna(), _textureSize);
                RenderingService.MaterialSetTextureTransform(_material, transform);
                _frameCounter = Mathz.Clamp(_frameCounter + 1, 0, _frames.Count - 1);
                _frameElapsed = TimeSpan.Zero;
            }
            

        }
        
        RenderingService.MaterialSetDiffuse(_material, Color.ToVector3());
        RenderingService.MaterialSetOpacity(_material, Color.A / 255f);
        RenderingService.MaterialSetCullMode(_material,
            DoubleSided ? CullMode.None : CullMode.CullCounterClockwiseFace);

        var rotation = Rotation;
        if (Billboard && CameraRid.IsValid)
        {
            var viewMatrix = RenderingService.CameraGetView(CameraRid);
            var invViewMatrix = Matrix.Invert(viewMatrix);
            var translation = invViewMatrix.Translation;

            var toCamera = (translation - Position) * new Vector3(1, 0, 1);
            var angleY = 0f;
            if (toCamera.LengthSquared() > 0.0001f)
            {
                toCamera.Normalize();
                angleY = (float)Math.Atan2(toCamera.X, toCamera.Z);
            }

            rotation = Quaternion.CreateFromAxisAngle(Vector3.Up, angleY);
        }

        Bounds = Mathz.ComputeBoundingBox(Position, Rotation, Scale, Size);
        RenderingService.InstanceSetPosition(Rid, Position);
        RenderingService.InstanceSetRotation(Rid, rotation);
        RenderingService.InstanceSetScale(Rid, Scale);
        UpdateMeshSurface();
    }

    private void UpdateMeshSurface()
    {
        RenderingService.MeshClear(_mesh);

        var halfSize = Size * 0.5f;
        var meshSurface = new MeshSurface
        {
            Vertices = new[]
            {
                new Vector3(-halfSize.X, -halfSize.Y, 0),
                new Vector3(halfSize.X, -halfSize.Y, 0),
                new Vector3(-halfSize.X, halfSize.Y, 0),
                new Vector3(halfSize.X, halfSize.Y, 0)
            },
            Normals = new[]
            {
                Vector3.Forward,
                Vector3.Forward,
                Vector3.Forward,
                Vector3.Forward
            },
            TexCoords = new[]
            {
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(0, 0),
                new Vector2(1, 0)
            },
            Indices = DoubleSided
                ? new[]
                {
                    // Front face triangles (counter-clockwise when viewed from front)
                    0, 1, 2, // Triangle 1
                    2, 1, 3, // Triangle 2

                    // Back face triangles (clockwise when viewed from front)
                    0, 2, 1, // Triangle 1 (reversed order)
                    2, 3, 1 // Triangle 2 (reversed order)
                }
                : new[]
                {
                    0, 1, 2, // Triangle 1
                    2, 1, 3 // Triangle 2
                }
        };

        RenderingService.MeshAddSurface(_mesh, PrimitiveType.TriangleList, meshSurface, _material);
    }

    public override void Dispose()
    {
        RenderingService.FreeRid(_mesh);
        RenderingService.FreeRid(_material);
        base.Dispose();
    }
}