using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace FezEditor.Services;

public interface IInputService
{
    void AddAction(string action, params Keys[] keys);
    
    void AddAction(string action, Keys key, bool ctrl = false, bool shift = false, bool alt = false);

    void EraseAction(string action);

    bool HasAction(string action);
    
    string GetActionBinding(string action, int index = 0);
    
    bool IsActionJustPressed(string action);
    
    bool IsActionPressed(string action);
    
    bool IsActionJustReleased(string action);
    
    float GetActionStrength(string action);
    
    float GetActionAxis(string negative, string positive);
    
    Vector2 GetActionsVector(string negativeX, string positiveX, string negativeY, string positiveY);

    bool IsRightMousePressed();
    
    bool IsLeftMousePressed();
    
    Vector2 GetMousePosition();
    
    Vector2 GetMouseDelta();

    void CaptureMouse(bool captured);
    
    void Update();
}