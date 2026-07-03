namespace FezEditor.Scripting;

[Entity(Static = true)]
internal interface ICamera
{
    [Description("When the viewpoint changed")]
    event Action Rotated;

    void OnRotate();

    [Description("Set the number of pixels per trixel (default is 4)")]
    void SetPixelsPerTrixel(int triles);

    [Description("Changes whether Gomez can rotate the camera")]
    void SetCanRotate(bool canRotate);

    [Description("Forces camera rotation, left (-1 to -3) or right (1 to 3)")]
    void Rotate(int distance);

    [Description("Rotates to any view orientation (Left, Right, Front, Back)")]
    void RotateTo(string viewName);

    [Description("Fades to the chosen color (Black, White, etc.)")]
    LongRunningAction FadeTo(string colorName);

    [Description("Fades from the chosen color (Black, White, etc.)")]
    LongRunningAction FadeFrom(string colorName);

    [Description("Flashes the chosen color once (Black, White, etc.)")]
    void Flash(string colorName);

    [Description("Shakes the camera and vibrates controller")]
    void Shake(float distance, float durationSeconds);

    [Description("Sets the camera offset as descending (true) or ascending (false)")]
    void SetDescending(bool descending);

    [Description("Remove the constraints (volume focus etc.)")]
    void Unconstrain();
}