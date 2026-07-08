using System.Text.Json.Serialization;
using FezEditor.Services;
using Microsoft.Xna.Framework;

namespace FezEditor.Structure;

[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public record Settings
{
    public static readonly string FilePath = Path.Combine(AppStorageService.BaseDir, "Settings.json");

    public List<RecentProvider> RecentProviders { get; init; } = new();

    public Dictionary<string, List<string>> RecentFiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<string>> ReferenceProviders { get; init; } = new();

    public WindowSize Window { get; init; } = new(1280, 720);

    public bool IsWindowMaximized { get; init; } = false;

    public Color[] PaintPalette { get; init; } = Array.Empty<Color>();

    public float? DisplayScale { get; init; } // null - automatic

    public string HatLauncherPath { get; init; } = "";

    public record struct RecentProvider(string Path, string Kind);

    public record struct WindowSize(int Width, int Height);
}
