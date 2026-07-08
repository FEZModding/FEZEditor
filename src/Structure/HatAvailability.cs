namespace FezEditor.Structure;

public abstract record HatAvailability
{
    public sealed record Available() : HatAvailability;

    public sealed record Unavailable(string Reason) : HatAvailability;
}