using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace FezEditor.Tools;

public static class ContentManagerExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };
    
    public static T LoadFromJson<T>(this ContentManager content, string assetName)
    {
        if (content is ZipContentManager zip)
        {
            return zip.LoadJson<T>(assetName);
        }

        var path = Path.Combine(content.RootDirectory, Path.ChangeExtension(assetName, ".json"));
        using var stream = TitleContainer.OpenStream(path);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)!;
    }
}