using FezEditor.Tools;
using ImGuiNET;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace FezEditor.Services;

[UsedImplicitly]
public class InputService : IInputService
{
    private readonly Game _game;

    private readonly Dictionary<string, List<Binding>> _bindings;

    private KeyboardState _currentKeyboardState;

    private KeyboardState _previousKeyboardState;

    private MouseState _currentMouseState;

    private MouseState _previousMouseState;

    private bool _mouseCaptured;

    public InputService(Game game)
    {
        _game = game;
        _bindings = game.Content.LoadFromJson<Dictionary<string, List<Binding>>>("InputActions");
    }

    public void AddAction(string action, params Keys[] keys)
    {
        GetLazyBindings(action).AddRange(keys.Select(k => new Binding(k)));
    }

    public void AddAction(string action, Keys key, bool ctrl = false, bool shift = false, bool alt = false)
    {
        GetLazyBindings(action).Add(new Binding(key, ctrl, shift, alt));
    }

    public void EraseAction(string action)
    {
        _bindings.Remove(action);
    }

    public bool HasAction(string action)
    {
        return _bindings.ContainsKey(action);
    }

    public bool IsActionJustPressed(string action)
    {
        return CheckAction(action, k => _currentKeyboardState.IsKeyDown(k) && _previousKeyboardState.IsKeyUp(k));
    }

    public bool IsActionPressed(string action)
    {
        return CheckAction(action, k => _currentKeyboardState.IsKeyDown(k));
    }

    public bool IsActionJustReleased(string action)
    {
        return CheckAction(action, k => _currentKeyboardState.IsKeyUp(k) && _previousKeyboardState.IsKeyDown(k));
    }

    public float GetActionStrength(string action)
    {
        return IsActionJustPressed(action) || IsActionPressed(action) ? 1f : 0f;
    }

    public float GetActionAxis(string negative, string positive)
    {
        return GetActionStrength(positive) - GetActionStrength(negative);
    }

    public Vector2 GetActionsVector(string negativeX, string positiveX, string negativeY, string positiveY)
    {
        var vector = new Vector2(GetActionAxis(negativeX, positiveX), GetActionAxis(negativeY, positiveY));
        if (!Mathz.IsZeroApprox(vector.X) || !Mathz.IsZeroApprox(vector.Y)) vector.Normalize();
        return vector;
    }

    public bool IsRightMousePressed()
    {
        return _currentMouseState.RightButton == ButtonState.Pressed;
    }

    public bool IsLeftMousePressed()
    {
        return _currentMouseState.LeftButton == ButtonState.Pressed;
    }

    public Vector2 GetMousePosition()
    {
        return new Vector2(_currentMouseState.X, _currentMouseState.Y);
    }

    public Vector2 GetMouseDelta()
    {
        var deltaX = _currentMouseState.X - _previousMouseState.X;
        var deltaY = _currentMouseState.Y - _previousMouseState.Y;
        return new Vector2(deltaX, deltaY);
    }

    public void CaptureMouse(bool captured)
    {
        _mouseCaptured = captured;
        ImGui.GetIO().WantCaptureMouse = !captured;
        _game.IsMouseVisible = !captured;
    }

    public void Update()
    {
        _previousKeyboardState = _currentKeyboardState;
        _currentKeyboardState = Keyboard.GetState();
        _previousMouseState = _currentMouseState;
        _currentMouseState = Mouse.GetState();
        if (_mouseCaptured && _game.IsActive)
        {
            Mouse.SetPosition(_game.Window.ClientBounds.Width / 2, _game.Window.ClientBounds.Height / 2);
        }
    }

    private List<Binding> GetLazyBindings(string action)
    {
        if (!_bindings.TryGetValue(action, out var value))
        {
            value = new List<Binding>();
            _bindings[action] = value;
        }

        return value;
    }

    private bool CheckAction(string action, Func<Keys, bool> check)
    {
        if (!_bindings.TryGetValue(action, out var list))
        {
            return false;
        }

        foreach (var b in list)
        {
            if (!check(b.Key)) continue;
            if (b.Ctrl && !IsCtrlDown()) continue;
            if (b.Shift && !IsShiftDown()) continue;
            if (b.Alt && !IsAltDown()) continue;
            return true;
        }

        return false;
    }

    private bool IsCtrlDown()
    {
        return _currentKeyboardState.IsKeyDown(Keys.LeftControl) || _currentKeyboardState.IsKeyDown(Keys.RightControl);
    }

    private bool IsShiftDown()
    {
        return _currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift);
    }

    private bool IsAltDown()
    {
        return _currentKeyboardState.IsKeyDown(Keys.LeftAlt) || _currentKeyboardState.IsKeyDown(Keys.RightAlt);
    }

    private record struct Binding(Keys Key, bool Ctrl = false, bool Shift = false, bool Alt = false);
}