using System.Text.Json;
using FezEditor.Tools;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FezEditor.Services;

[UsedImplicitly]
public class ExportService : IDisposable
{
    private static readonly ILogger Logger = Logging.Create<ExportService>();

    private const string CurrentStateFile = "ExportState.json";

    public event Action<string, Texture2D>? TextureReloaded;

    private readonly ResourceService _resources;

    private readonly Game _game;

    private Dictionary<string, ExportedTexture> _exportedTextures = new();

    public ExportService(Game game)
    {
        _game = game;
        _game.Activated += ReloadTextures;
        _resources = game.GetService<ResourceService>();
        CleanUpOrphanTextures();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _game.Activated -= ReloadTextures;
    }

    public void ExportTexture(string path, Texture2D texture)
    {
        if (_resources.IsReadonly)
        {
            return;
        }

        var fullPath = _resources.GetFullPath(path);
        if (string.IsNullOrEmpty(fullPath))
        {
            return;
        }

        var rgba = new byte[texture.Width * texture.Height * 4];
        texture.GetData(rgba);
        for (var i = 3; i < rgba.Length; i += 4)
        {
            rgba[i] = (byte)(255 - rgba[i]); // Invert alpha channel
        }

        using var image = Image.LoadPixelData<Rgba32>(rgba, texture.Width, texture.Height);
        var pngPng = Path.ChangeExtension(fullPath, ".tmp.png");
        image.SaveAsPng(pngPng);

        _exportedTextures[path] = new ExportedTexture(pngPng, File.GetLastWriteTime(pngPng));
        WriteCurrentState();
    }

    public void UntrackTexture(string path)
    {
        if (_exportedTextures.Remove(path, out var texture))
        {
            WriteCurrentState();
            File.Delete(texture.Path);
        }
    }

    private void ReloadTextures(object? sender, EventArgs e)
    {
        if (_resources.IsReadonly)
        {
            return;
        }

        foreach (var (assetPath, exported) in _exportedTextures)
        {
            if (!File.Exists(exported.Path))
            {
                continue;
            }

            var lastWrite = File.GetLastWriteTime(exported.Path);
            if (lastWrite <= exported.LastWrite)
            {
                continue;
            }

            _exportedTextures[assetPath] = exported with { LastWrite = lastWrite };
            WriteCurrentState();

            using var image = Image.Load<Rgba32>(exported.Path);
            var rgba = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(rgba);

            var newTexture = new Texture2D(_game.GraphicsDevice, image.Width, image.Height, false, SurfaceFormat.Color);
            newTexture.SetData(rgba);
            TextureReloaded?.Invoke(assetPath, newTexture);
        }
    }

    private void CleanUpOrphanTextures()
    {
        ReadCurrentState();

        var keysToRemove = _exportedTextures.Keys.ToList();
        foreach (var key in keysToRemove)
        {
            var texture = _exportedTextures[key].Path;
            File.Delete(texture);
            _exportedTextures.Remove(key);
        }

        WriteCurrentState();
    }

    private void ReadCurrentState()
    {
        if (File.Exists(CurrentStateFile))
        {
            try
            {
                using var fileStream = File.OpenRead(CurrentStateFile);
                _exportedTextures = JsonSerializer.Deserialize<Dictionary<string, ExportedTexture>>(fileStream)!;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to read previous exported state.");
            }
        }
    }

    private void WriteCurrentState()
    {
        if (_exportedTextures.Count == 0)
        {
            File.Delete(CurrentStateFile);
            return;
        }

        using var fileStream = File.Create(CurrentStateFile);
        JsonSerializer.Serialize(fileStream, _exportedTextures);
    }

    private record ExportedTexture(string Path, DateTime LastWrite);
}