using System.Security.Cryptography;
using System.Text;
using FezEditor.Services;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace FezEditor.Tools;

public class Thumbnailer
{
    private const int BytesPerPixel = 4;

    private readonly RTexture2D _source;

    private readonly string _thumbPath;

    private readonly DateTime _lastWrite;

    private readonly string _path;

    public Thumbnailer(string path, DateTime lastWrite, ArtObject ao) : this(path, lastWrite)
    {
        var cubemap = ao.Cubemap;
        if (cubemap == null)
        {
            return;
        }

        var faceWidth = cubemap.Width / 6;
        var faceRect = new Rectangle(0, 0, faceWidth, cubemap.Height);
        var data = CropRawRegion(cubemap.TextureData, cubemap.Width, faceRect);
        SetOpaqueAlpha(data);
        _source = new RTexture2D
        {
            Width = faceWidth,
            Height = cubemap.Height,
            TextureData = data
        };
    }

    public Thumbnailer(string path, DateTime lastWrite, Trile trile, RTexture2D? atlas) : this(path, lastWrite)
    {
        if (atlas == null)
        {
            return;
        }

        var px = (int)MathF.Round(trile.AtlasOffset.X * atlas.Width);
        var py = (int)MathF.Round(trile.AtlasOffset.Y * atlas.Height);
        var rect = new Rectangle(px + 1, py + 1, 16, 16);
        var data = CropRawRegion(atlas.TextureData, atlas.Width, rect);
        SetOpaqueAlpha(data);
        _source = new RTexture2D
        {
            Width = 16,
            Height = 16,
            TextureData = data
        };
    }

    public Thumbnailer(string path, DateTime lastWrite, RTexture2D texture) : this(path, lastWrite)
    {
        _source = texture;
    }

    public Thumbnailer(string path, DateTime lastWrite, RAnimatedTexture anim) : this(path, lastWrite)
    {
        var frame = anim.Frames[0].Rectangle.ToXna();
        _source = new RTexture2D
        {
            Width = frame.Width,
            Height = frame.Height,
            TextureData = CropRawRegion(anim.TextureData, anim.AtlasWidth, frame)
        };
    }

    public Thumbnailer(string path, DateTime lastWrite)
    {
        _lastWrite = lastWrite;
        _path = path;
        _source = new RTexture2D();
        {
            var normalizedPath = path.ToLowerInvariant().Replace('\\', '/');
            var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(normalizedPath));
            var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            _thumbPath = $"thumb-{hash}.png";
        }
    }

    public bool IsCacheCurrent()
    {
        return AppStorageService.HasCacheFile(_thumbPath) &&
               ThumbnailDatabase.IsThumbnailCurrent(_path, _lastWrite);
    }

    public bool TryLoad(out RTexture2D? texture)
    {
        if (!IsCacheCurrent())
        {
            texture = null;
            return false;
        }

        using var image = Image.Load<Rgba32>(AppStorageService.LoadFromCache(_thumbPath));
        var data = new byte[image.Width * image.Height * BytesPerPixel];
        image.CopyPixelDataTo(data);

        texture = new RTexture2D
        {
            Width = image.Width,
            Height = image.Height,
            TextureData = data
        };
        return true;
    }

    public RTexture2D Generate()
    {
        using var image = Image.LoadPixelData<Rgba32>(_source.TextureData, _source.Width, _source.Height);
        var data = new byte[_source.Width * _source.Height * BytesPerPixel];
        image.CopyPixelDataTo(data);

        return new RTexture2D
        {
            Width = image.Width,
            Height = image.Height,
            TextureData = data
        };
    }

    public void Save(RTexture2D texture)
    {
        #region Thumbnail

        {
            using var image = Image.LoadPixelData<Rgba32>(texture.TextureData, texture.Width, texture.Height);
            using var png = new MemoryStream();
            image.SaveAsPng(png);
            AppStorageService.SaveToCache(_thumbPath, png);
        }

        #endregion

        ThumbnailDatabase.SetThumbnailCurrent(_path, _lastWrite);
    }

    private static void SetOpaqueAlpha(byte[] data)
    {
        for (var i = 3; i < data.Length; i += BytesPerPixel)
        {
            data[i] = 255;
        }
    }

    private static byte[] CropRawRegion(byte[] data, int stride, Rectangle rect)
    {
        var result = new byte[rect.Width * rect.Height * BytesPerPixel];
        for (var row = 0; row < rect.Height; row++)
        {
            var srcOffset = (((rect.Y + row) * stride) + rect.X) * BytesPerPixel;
            var dstOffset = row * rect.Width * BytesPerPixel;
            Buffer.BlockCopy(data, srcOffset, result, dstOffset, rect.Width * BytesPerPixel);
        }

        return result;
    }
}