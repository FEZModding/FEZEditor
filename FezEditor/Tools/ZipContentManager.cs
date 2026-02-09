using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework.Content;

namespace FezEditor.Tools;

public class ZipContentManager : ContentManager
{
    private readonly ZipArchive _archive;
    
    public ZipContentManager(IServiceProvider serviceProvider, string zipPath)
        : base(serviceProvider)
    {
        _archive = ZipFile.OpenRead(zipPath);
        CheckContentsVersion();
    }
    
    public T LoadJson<T>(string assetName)
    {
        var path = Path.ChangeExtension(assetName, ".json");
        var entry = _archive.GetEntry(path)!;
        using var stream = entry.Open();
        return JsonSerializer.Deserialize<T>(stream)!;
    }

    protected override Stream OpenStream(string assetName)
    {
        var entry = _archive.Entries
            .First(e => e.FullName.StartsWith(assetName, StringComparison.Ordinal));
        
        var memoryStream = new MemoryStream();
        using (var stream = entry.Open())
        {
            stream.CopyTo(memoryStream);
        }
        memoryStream.Position = 0;
        
        return memoryStream;
    }

    private void CheckContentsVersion()
    {
        var entry = _archive.GetEntry(".version")!;
        
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var rule = reader.ReadToEnd().Trim();
        
        string op;
        string versionStr;
    
        if (rule.StartsWith(">="))
        {
            op = ">=";
            versionStr = rule[2..].Trim();
        }
        else if (rule.StartsWith("<="))
        {
            op = "<=";
            versionStr = rule[2..].Trim();
        }
        else if (rule.StartsWith("=="))
        {
            op = "==";
            versionStr = rule[2..].Trim();
        }
        else if (rule.StartsWith('>'))
        {
            op = ">";
            versionStr = rule[1..].Trim();
        }
        else if (rule.StartsWith('<'))
        {
            op = "<";
            versionStr = rule[1..].Trim();
        }
        else
        {
            op = "==";
            versionStr = rule;
        }

        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
        var contentsVersion = Version.Parse(versionStr);
        
        var compatible = op switch
        {
            ">=" => contentsVersion >= assemblyVersion,
            "<=" => contentsVersion <= assemblyVersion,
            "==" => contentsVersion == assemblyVersion,
            ">" => contentsVersion > assemblyVersion,
            "<" => contentsVersion < assemblyVersion,
            _ => false
        };

        if (!compatible)
        {
            throw new NotSupportedException($"Invalid version: {rule}, requires: >={assemblyVersion}");
        }
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