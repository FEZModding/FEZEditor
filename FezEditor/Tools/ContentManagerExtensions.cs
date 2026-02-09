using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace FezEditor.Tools;

public static class ContentManagerExtensions
{
    public static T LoadFromJson<T>(this ContentManager content, string assetName)
    {
        if (content is ZipContentManager zip)
        {
            return zip.LoadJson<T>(assetName);
        }

        using var stream = TitleContainer.OpenStream(Path.ChangeExtension(assetName, ".json"));
        return JsonSerializer.Deserialize<T>(stream)!;
    }
}