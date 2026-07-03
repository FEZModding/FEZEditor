namespace FezEditor.Scripting;

[Entity(Static = true)]
internal interface ITime
{
    [Description("The hour of day (0-23)")]
    int Hour { get; }

    [Description("Changes the hour of day (0-23), gradually or immediately")]
    LongRunningAction SetHour(int hour, bool immediate);

    [Description("Sets the speed of time passage (0 = paused)")]
    void SetTimeFactor(int factor);

    [Description("Increments the time factor (specifying how much time before it doubles up)")]
    LongRunningAction IncrementTimeFactor(float secondsUntilDouble);
}