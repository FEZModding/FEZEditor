namespace FezEditor.Tools;

public static class NullableExtensions
{
    public static string? NullIfEmpty(this string instance)
    {
        return string.IsNullOrEmpty(instance) ? null : instance;
    }

    public static string EmptyIfNull(this string? instance)
    {
        return instance ?? "";
    }

    public static T[]? NullIfEmpty<T>(this T[] instance)
    {
        return instance is not { Length: > 0 } ? null : instance;
    }

    public static T[] EmptyIfNull<T>(this T[]? instance)
    {
        return instance ?? [];
    }

    public static List<T>? NullIfEmpty<T>(this List<T> instance)
    {
        return instance is not { Count: > 0 } ? null : instance;
    }

    public static List<T> EmptyIfNull<T>(this List<T>? instance)
    {
        return instance ?? [];
    }
}