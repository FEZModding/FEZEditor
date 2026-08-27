using FezEditor.Services;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class OrbitControl : ActorComponent
{
    private const float MouseSensitivity = 0.005f;

    private delegate bool CaptureMouseDelta(out Vector2 delta);

    public float Yaw { get; set; } // 0f

    public float Pitch
    {
        get;
        set => field = MathHelper.Clamp(value, PitchClamp.X + 0.01f, PitchClamp.Y - 0.01f);
    }

    public Vector2 PitchClamp { get; set; } = new Vector2(-1f, 1f) * MathHelper.PiOver2;

    public bool UseRightMouseButton { get; set; } = false;

    private readonly InputService _input;

    private readonly Transform _transform;

    internal OrbitControl(Game game, Actor actor) : base(game, actor)
    {
        _input = game.GetService<InputService>();
        _transform = actor.GetComponent<Transform>();
    }

    public override void Update(GameTime gameTime)
    {
        Hints?.Add(UseRightMouseButton ? "RMB" : "MMB", "Orbit");
        CaptureMouseDelta captureMouseDelta = UseRightMouseButton
            ? _input.CaptureRightMouseDelta
            : _input.CaptureMiddleMouseDelta;

        if (captureMouseDelta(out var delta))
        {
            Yaw -= delta.X * MouseSensitivity;
            Pitch -= delta.Y * MouseSensitivity;
        }

        _transform.Rotation = Quaternion.CreateFromYawPitchRoll(Yaw, Pitch, 0f);
    }
}