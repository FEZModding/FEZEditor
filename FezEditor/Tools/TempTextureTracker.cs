using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FezEditor.Tools;

public sealed class TempTextureTracker : IDisposable
{
    private const int BytesPerPixel = 4;

    private readonly string _tempFilePath;

    private DateTime _lastWrite;

    public event Action<Texture2D>? Changed;

    private readonly Game _game;

    public TempTextureTracker(Game game, Texture2D source, string tempFilePath)
    {
        _game = game;
        _game.Activated += OnWindowFocused;
        _tempFilePath = Path.ChangeExtension(tempFilePath, ".tmp.png");
        WriteToDisk(source);
    }

    public void OpenInEditor()
    {
        using var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = _tempFilePath;
        process.StartInfo.UseShellExecute = true;
        process.Start();
    }

    public void Dispose()
    {
        _game.Activated -= OnWindowFocused;
        File.Delete(_tempFilePath);
    }

    public static void CleanOrphans(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException();
        }

        var files = Directory.EnumerateFiles(path, "*.tmp.png", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            File.Delete(file);
        }
    }

    private void OnWindowFocused(object? sender, EventArgs e)
    {
        if (!File.Exists(_tempFilePath))
        {
            throw new FileNotFoundException();
        }

        var lastWrite = File.GetLastWriteTime(_tempFilePath);
        if (lastWrite <= _lastWrite)
        {
            return;
        }

        _lastWrite = lastWrite;
        Changed?.Invoke(LoadFromDisk());
    }

    private void WriteToDisk(Texture2D texture)
    {
        var rgba = new byte[texture.Width * texture.Height * BytesPerPixel];
        texture.GetData(rgba);

        using var image = Image.LoadPixelData<Rgba32>(rgba, texture.Width, texture.Height);
        image.SaveAsPng(_tempFilePath);

        _lastWrite = File.GetLastWriteTime(_tempFilePath);
    }

    private Texture2D LoadFromDisk()
    {
        using var image = Image.Load<Rgba32>(_tempFilePath);
        var rgba = new byte[image.Width * image.Height * BytesPerPixel];
        image.CopyPixelDataTo(rgba);

        var texture = new Texture2D(_game.GraphicsDevice, image.Width, image.Height, false, SurfaceFormat.Color);
        texture.SetData(rgba);
        return texture;
    }
}