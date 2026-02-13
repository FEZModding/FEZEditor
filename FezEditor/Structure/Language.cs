namespace FezEditor.Structure;

public enum Language
{
    English,
    French,
    Italian,
    German,
    Spanish,
    Portuguese,
    Japanese,
    Korean,
    Chinese
}

public static class LanguageExtensions
{
    private static readonly Dictionary<string, Language> LanguageKeys = new()
    {
        [""] = Language.English,
        ["fr"] = Language.French,
        ["it"] = Language.Italian,
        ["de"] = Language.German,
        ["es"] = Language.Spanish,
        ["pt"] = Language.Portuguese,
        ["ja"] = Language.Japanese,
        ["ko"] = Language.Korean,
        ["zh"] = Language.Chinese
    };

    public static string GetId(this Language language)
    {
        return LanguageKeys.FirstOrDefault(kv => kv.Value == language).Key;
    }
}