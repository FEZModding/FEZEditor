using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Hosts;

public class FirstPersonControl
{
    public float MovementSpeed { get; set; } = 8.0f;

    public float MouseSensitivity { get; set; } = 0.002f;

    public CameraHost Camera { get; init; } = null!;

    private float _yaw;

    private float _pitch;

    private readonly IInputService _inputService;

    private readonly Game _game;

    public FirstPersonControl(Game game)
    {
        _game = game;
        _inputService = game.GetService<IInputService>();
    }

    public void Update(GameTime gameTime)
    {
        #region Handle mouse input
        
        _inputService.CaptureMouse(false);
        if (_inputService.IsRightMousePressed())
        {
            var delta = _inputService.GetMouseDelta();
            _yaw -= delta.X * MouseSensitivity;
            _pitch += delta.Y * MouseSensitivity;
            _pitch = MathHelper.Clamp(_pitch, -MathHelper.PiOver2 + 0.01f, MathHelper.PiOver2 - 0.01f);
            _inputService.CaptureMouse(true);
        }
        
        #endregion
        
        #region Handle key input
        
        var inputDirection = _inputService.GetActionsVector(
            negativeX: InputActions.MoveLeft,
            positiveX: InputActions.MoveRight,
            negativeY: InputActions.MoveBackward,
            positiveY: InputActions.MoveForward
        );
        
        var rotation = Camera.Rotation;
        var forward = Vector3.Transform(Vector3.Forward, rotation);
        var right = Vector3.Transform(Vector3.Right, rotation);
        if (forward.LengthSquared() > 0) forward.Normalize();
        if (right.LengthSquared() > 0) right.Normalize();
        var direction = (forward * inputDirection.Y + right * inputDirection.X);
        
        #endregion

        #region Apply movement

        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        Camera.Position += direction * MovementSpeed * deltaTime;

        #endregion

        #region Update rotation

        var yawQuaternion = Quaternion.CreateFromAxisAngle(Vector3.Up, _yaw);
        var pitchQuaternion = Quaternion.CreateFromAxisAngle(Vector3.Right, _pitch);
        Camera.Rotation = yawQuaternion * pitchQuaternion;

        #endregion

        Camera.Update(gameTime);
    }
}