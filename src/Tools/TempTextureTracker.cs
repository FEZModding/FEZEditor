using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FezEditor.Tools;

public sealed class TempTextureTracker : IDisposable
{
    private const int BytesPerPixel = 4;

    private readonly string _filePath;

    private readonly bool _isTemporary;

    private DateTime _lastWrite;

    public event Action<Texture2D>? Changed;

    private readonly Game _game;

    public TempTextureTracker(Game game, Texture2D source, string tempFilePath)
    {
        _game = game;
        _game.Activated += OnWindowFocused;
        _filePath = Path.ChangeExtension(tempFilePath, ".tmp.png");
        _isTemporary = true;
        WriteToDisk(source);
    }

    public TempTextureTracker(Game game, string filePath)
    {
        _game = game;
        _game.Activated += OnWindowFocused;
        _filePath = filePath;
        _isTemporary = false;
        _lastWrite = File.GetLastWriteTimeUtc(_filePath);
    }

    public void OpenInEditor()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo(_filePath) { UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", _filePath);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", _filePath);
        }
    }

    public void Dispose()
    {
        _game.Activated -= OnWindowFocused;
        if (_isTemporary)
        {
            File.Delete(_filePath);
        }
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
        if (!File.Exists(_filePath))
        {
            if (_isTemporary)
            {
                throw new FileNotFoundException();
            }

            return;
        }

        var lastWrite = File.GetLastWriteTimeUtc(_filePath);
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
        image.SaveAsPng(_filePath);

        _lastWrite = File.GetLastWriteTimeUtc(_filePath);
    }

    private Texture2D LoadFromDisk()
    {
        using var image = Image.Load<Rgba32>(_filePath);
        var rgba = new byte[image.Width * image.Height * BytesPerPixel];
        image.CopyPixelDataTo(rgba);

        var texture = new Texture2D(_game.GraphicsDevice, image.Width, image.Height, false, SurfaceFormat.Color);
        texture.SetData(rgba);
        return texture;
    }
}
