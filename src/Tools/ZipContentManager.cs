using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework.Content;
using Serilog;

namespace FezEditor.Tools;

public class ZipContentManager : ContentManager, IContentManager
{
    private static readonly ILogger Logger = Log.ForContext<ZipContentManager>();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly ZipArchive _archive;

    public ZipContentManager(IServiceProvider serviceProvider, Stream stream) : base(serviceProvider)
    {
        _archive = new ZipArchive(stream, ZipArchiveMode.Read);
    }

    public T LoadJson<T>(string assetName)
    {
        var path = Path.ChangeExtension(assetName, ".json");
        var entry = _archive.GetEntry(path)!;
        using var stream = entry.Open();
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)!;
    }

    public byte[] LoadBytes(string assetName)
    {
        // DeflateStream doesn't support Length property
        using var stream = LoadStream(assetName);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    public Stream LoadStream(string assetName)
    {
        var entry = _archive.GetEntry(assetName);
        if (entry != null)
        {
            return entry.Open();
        }

        foreach (var candidate in _archive.Entries)
        {
            var path = Path.ChangeExtension(candidate.FullName, null);
            if (!path.Equals(assetName, StringComparison.Ordinal))
            {
                continue;
            }

            if (entry != null)
            {
                throw new FileNotFoundException($"Asset name is ambiguous in content bundle: {assetName}");
            }

            entry = candidate;
        }

        if (entry == null)
        {
            throw new FileNotFoundException($"Asset not found in content bundle: {assetName}");
        }

        return entry.Open();
    }

    protected override Stream OpenStream(string assetName)
    {
        Logger.Debug("Loading asset - {0}", assetName);
        using var stream = LoadStream(assetName);
        var memory = new MemoryStream();
        stream.CopyTo(memory);
        memory.Position = 0;
        return memory;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _archive.Dispose();
        }

        base.Dispose(disposing);
    }
}