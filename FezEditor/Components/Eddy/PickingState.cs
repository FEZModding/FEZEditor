namespace FezEditor.Components.Eddy;

public abstract record PickingState
{
    private PickingState() { }

    public sealed record None : PickingState;

    public sealed record Waiting : PickingState;

    public sealed record Picked(InstanceId Instance) : PickingState;
}