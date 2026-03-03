using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class FirstPersonControl : ActorComponent
{
    public float MovementSpeed { get; set; } = 8.0f;

    public float MouseSensitivity { get; set; } = 0.002f;

    private float _yaw;

    private float _pitch;

    private readonly InputService _input;

    private readonly Transform _transform;

    internal FirstPersonControl(Game game, Actor actor) : base(game, actor)
    {
        _input = game.GetService<InputService>();
        _transform = actor.GetComponent<Transform>();
    }

    public override void Update(GameTime gameTime)
    {
        #region Handle mouse input

        _input.CaptureMouse(false);
        if (_input.IsRightMousePressed())
        {
            var delta = _input.GetMouseDelta();
            _yaw -= delta.X * MouseSensitivity;
            _pitch -= delta.Y * MouseSensitivity;
            _pitch = MathHelper.Clamp(_pitch, -MathHelper.PiOver2 + 0.01f, MathHelper.PiOver2 - 0.01f);
            _input.CaptureMouse(true);
        }

        #endregion

        #region Handle key input

        var inputDirection = _input.GetActionsVector(
            InputActions.MoveLeft,
            InputActions.MoveRight,
            InputActions.MoveBackward,
            InputActions.MoveForward
        );

        var rotation = _transform.Rotation;
        var forward = Vector3.Transform(Vector3.Forward, rotation);
        var right = Vector3.Transform(Vector3.Right, rotation);
        if (forward.LengthSquared() > 0)
        {
            forward.Normalize();
        }

        if (right.LengthSquared() > 0)
        {
            right.Normalize();
        }

        var direction = (forward * inputDirection.Y) + (right * inputDirection.X);

        #endregion

        #region Apply movement

        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _transform.Position += direction * MovementSpeed * deltaTime;

        #endregion

        #region Update rotation

        var yawQuaternion = Quaternion.CreateFromAxisAngle(Vector3.Up, _yaw);
        var pitchQuaternion = Quaternion.CreateFromAxisAngle(Vector3.Right, _pitch);
        _transform.Rotation = yawQuaternion * pitchQuaternion;

        #endregion
    }
}