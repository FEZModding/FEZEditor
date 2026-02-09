using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace FezEditor.Hosts;

public class FirstPersonControl
{
    public float MovementSpeed { get; set; } = 8.0f;

    public float MouseSensitivity { get; set; } = 0.002f;

    public CameraHost Camera { get; init; } = null!;

    private float _yaw;

    private float _pitch;

    private MouseState _previousMouseState;

    private readonly Game _game;

    public FirstPersonControl(Game game)
    {
        _game = game;
        _previousMouseState = Mouse.GetState();
    }

    public void Update(GameTime gameTime)
    {
        #region Handle mouse input
        
        var currentMouseState = Mouse.GetState();
        SetMouseVisibility(true);
        
        if (currentMouseState.RightButton == ButtonState.Pressed)
        {
            var deltaX = currentMouseState.X - _previousMouseState.X;
            var deltaY = currentMouseState.Y - _previousMouseState.Y;
            _yaw -= deltaX * MouseSensitivity;
            _pitch += deltaY * MouseSensitivity;
            
            _pitch = MathHelper.Clamp(_pitch, -MathHelper.PiOver2 + 0.01f,
                MathHelper.PiOver2 - 0.01f);
            
            // This prevents the mouse from leaving the window
            if (_game.IsActive)
            {
                Mouse.SetPosition(_game.Window.ClientBounds.Width / 2, _game.Window.ClientBounds.Height / 2);
                _previousMouseState = Mouse.GetState();
                SetMouseVisibility(false);
            }
        }
        
        #endregion
        
        #region Handle key input

        var currentKeyboardState = Keyboard.GetState();
        
        var direction = Vector3.Zero;
        if (currentKeyboardState.IsKeyDown(Keys.W)) direction += Vector3.Forward;
        if (currentKeyboardState.IsKeyDown(Keys.S)) direction += Vector3.Backward;
        if (currentKeyboardState.IsKeyDown(Keys.A)) direction += Vector3.Right;
        if (currentKeyboardState.IsKeyDown(Keys.D)) direction += Vector3.Left;
        if (direction != Vector3.Zero) direction.Normalize();
        
        var rotation = Camera.Rotation;
        var forward = Vector3.Transform(Vector3.Forward, rotation);
        var right = Vector3.Transform(Vector3.Right, rotation);
        if (forward.LengthSquared() > 0) forward.Normalize();
        if (right.LengthSquared() > 0) right.Normalize();
        direction = (forward * direction.Z + right * direction.X);
        
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
        _previousMouseState = Mouse.GetState();
    }

    private void SetMouseVisibility(bool enabled)
    {
        ImGui.GetIO().WantCaptureMouse = enabled;
        _game.IsMouseVisible = enabled;
    }
}