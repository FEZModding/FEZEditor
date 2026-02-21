using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class Camera : ActorComponent
{
    public enum ProjectionType
    {
        Perspective,
        Orthographic
    }

    public ProjectionType Projection { get; set; } = ProjectionType.Perspective;

    public float FieldOfView
    {
        get => _fieldOfView;
        set
        {
            if (Projection != ProjectionType.Perspective)
            {
                throw new ArgumentException("Projection type must be perspective!");
            }

            _fieldOfView = value;
        }
    }

    public float Size
    {
        get => _size;
        set
        {
            if (Projection != ProjectionType.Orthographic)
            {
                throw new ArgumentException("Projection type must be orthographic!");
            }

            _size = value;
        }
    }

    public float Near { get; set; } = 0.05f;

    public float Far { get; set; } = 1000.0f;

    private RenderingService _rendering = null!;

    private Rid _camera;

    private Rid _rt;

    private float _size = 1.0f;

    private float _fieldOfView = 75.0f;

    public override void Initialize()
    {
        _rendering = Game.GetService<RenderingService>();
        var world = _rendering.InstanceGetWorld(Actor.InstanceRid);
        if (_rendering.WorldHasCamera(world))
        {
            throw new InvalidOperationException("A single camera was already initialized!");
        }

        _camera = _rendering.CameraCreate();
        _rendering.WorldSetCamera(world, _camera);
        _rt = _rendering.WorldGetRenderTarget(world);
    }

    public override void Update(GameTime gameTime)
    {
        var world = _rendering.InstanceGetWorldMatrix(Actor.InstanceRid);
        var viewMatrix = Matrix.CreateLookAt(world.Translation, world.Translation + world.Forward, world.Up);

        var (width, height) = _rendering.RenderTargetGetSize(_rt);
        var aspectRatio = (float)width / height;
        var projectionMatrix = Projection switch
        {
            ProjectionType.Perspective => Matrix
                .CreatePerspectiveFieldOfView(MathHelper.ToRadians(FieldOfView), aspectRatio, Near, Far),

            ProjectionType.Orthographic => Matrix
                .CreateOrthographic(aspectRatio * Size, Size, Near, Far),

            _ => Matrix.Identity
        };

        _rendering.CameraSetView(_camera, viewMatrix);
        _rendering.CameraSetProjection(_camera, projectionMatrix);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_camera);
    }
}