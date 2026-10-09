using System.Xml;
using System.Xml.Serialization;

namespace FezEditor.Structure;

// Editor-owned copy of HAT's World.xml contract; no HAT runtime dependency.
[XmlRoot("WorldMetadata")]
public class WorldMetadata
{
    public const string ResourcePath = "mod://World.xml";

    public const string FileName = "World.xml";

    public string? DisplayName { get; set; }

    public string? Description { get; set; }

    public string? MapTree { get; set; }

    public string? Thumbnail { get; set; }

    public string? StartingLevel { get; set; }

    [XmlArrayItem("Field")] public List<SaveFieldDefinition>? StartingSaveFields { get; set; }

    [XmlArrayItem("Level")] public List<string>? AlwaysBlackHoleLevels { get; set; }

    [XmlArrayItem("Line")] public List<string>? DotCensorship { get; set; }

    public class SaveFieldDefinition
    {
        [XmlAttribute] public string? Name { get; set; }

        [XmlAttribute] public string? Value { get; set; }
    }

    public static bool IsResourcePath(string path)
    {
        return string.Equals(path, ResourcePath, StringComparison.OrdinalIgnoreCase);
    }

    public static WorldMetadata Read(Stream stream)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        var serializer = new XmlSerializer(typeof(WorldMetadata));
        var world = serializer.Deserialize(reader) as WorldMetadata;
        return world ?? throw new InvalidOperationException("The world file is invalid.");
    }

    public void Write(Stream stream)
    {
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Indent = true });
        var serializer = new XmlSerializer(typeof(WorldMetadata));
        var namespaces = new XmlSerializerNamespaces();
        namespaces.Add(string.Empty, string.Empty);
        serializer.Serialize(writer, this, namespaces);
    }
}
