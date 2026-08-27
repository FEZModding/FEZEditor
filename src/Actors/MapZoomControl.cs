using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class MapZoomControl : ActorComponent
{
    private static readonly int[] PixelsPerTrileSteps = [4, 8, 16, 32, 64];

    private const float LerpSpeed = 10f;

    public int PixelsPerTrile => PixelsPerTrileSteps[_zoomIndex];

    private readonly InputService _input;

    private readonly RenderingService _rendering;

    private readonly Camera _camera;

    private readonly Rid _rt;

    private int _zoomIndex = 2;

    private float? _sizeOverride;

    public MapZoomControl(Game game, Actor actor) : base(game, actor)
    {
        _input = game.GetService<InputService>();
        _rendering = game.GetService<RenderingService>();
        _camera = actor.GetComponent<Camera>();
        _rt = _rendering.WorldGetRenderTarget(_rendering.InstanceGetWorld(actor.InstanceRid));
        _camera.Projection = Camera.ProjectionType.Orthographic;
        _camera.Size = ComputeTargetSize();
    }

    public void Reset()
    {
        _zoomIndex = 2;
        _sizeOverride = null;
    }

    public void SetOverride(float size)
    {
        _sizeOverride = size;
        _camera.Size = size;
    }

    public void ClearOverride()
    {
        _sizeOverride = null;
        _camera.Size = ComputeTargetSize();
    }

    public override void Update(GameTime gameTime)
    {
        Hints?.Add("Scroll Wheel", "Cycle Zoom");
        var targetSize = _sizeOverride ?? ComputeTargetSize();
        if (MathF.Abs(_camera.Size - targetSize) > 0.01f)
        {
            var delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _camera.Size = MathHelper.Lerp(_camera.Size, targetSize, LerpSpeed * delta);
        }
        else
        {
            _camera.Size = targetSize;
        }

        if (!_sizeOverride.HasValue && _input.CaptureScrollWheelDelta(out var scroll))
        {
            _zoomIndex = Math.Clamp(_zoomIndex + Math.Sign(scroll), 0, PixelsPerTrileSteps.Length - 1);
        }
    }

    private float ComputeTargetSize()
    {
        var (_, height) = _rendering.RenderTargetGetSize(_rt);
        return height > 0 ? height / (float)PixelsPerTrile : 20f;
    }
}