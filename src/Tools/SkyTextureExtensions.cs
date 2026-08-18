using System.Diagnostics.CodeAnalysis;
using FezEditor.Services;

namespace FezEditor.Tools;

public static class SkyTextureExtensions
{
    public static bool TryLoadSkyTexture(
        this ResourceService resources,
        string sky,
        string? textureName,
        [NotNullWhen(true)] out RTexture2D? texture
    ) {
        if (string.IsNullOrEmpty(sky) || string.IsNullOrEmpty(textureName))
        {
            texture = null;
            return false;
        }

        try
        {
            texture = resources.Load<RTexture2D>($"Skies/{sky}/{textureName}");
            return true;
        }
        catch (FileNotFoundException ex)
        {
            texture = null;
            return false;
        }
    }
}