namespace FezEditor.Scripting;

[Entity(Static = true)]
internal interface IGame
{
    [Description("Is there an open scroll?")]
    bool IsScrollOpen { get; }

    string GetGlobalState { get; }

    string GetLevelState { get; }

    bool IsMapQrResolved { get; }

    bool IsSewerQrResolved { get; }

    bool IsZuQrResolved { get; }

    [Description("Stops the trial EXPERIENCE and requires that the game is bought to continue")]
    void EndTrial(bool forceRestart);

    [Description("Pauses the script for some time")]
    LongRunningAction Wait(float seconds);

    [Description("Glitch up")]
    LongRunningAction GlitchUp();

    [Description("Reboots")]
    LongRunningAction Reboot(string toLevel);

    [Description("Changes gravity")]
    void SetGravity(bool inverted, float factor);

    [Description("Show scroll with localized string, for some time or indefinitely (0 or less), at the top or the bottom of the screen")]
    LongRunningAction ShowScroll(string localizedString, float forSeconds, bool onTop, bool onVolume);

    [Description("Hides the current scroll immediately")]
    void CloseScroll(string key);

    [Description("Sets a state string that is kept between levels")]
    void SetGlobalState(string state);

    [Description("Sets a state string that is local to that level")]
    void SetLevelState(string state);

    void AllowMapUsage();

    void Start32BitCutscene();

    void Start64BitCutscene();

    void Checkpoint();

    void ResolveMapQR();

    void ResolveSewerQR();

    void ResolveZuQR();

    void ShowCapsuleLetter();
}