using FezEditor.Components;
using FezEditor.Structure;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.MapTree;
using FEZRepacker.Core.Definitions.Game.NpcMetadata;
using FEZRepacker.Core.Definitions.Game.Sky;
using FEZRepacker.Core.Definitions.Game.TrackedSong;
using FEZRepacker.Core.Definitions.Game.TrileSet;

namespace FezEditor.Services;

public partial class EditorService
{
    private static readonly Dictionary<string, Type> AssetTypes = new()
    {
        ["Art Object"] = typeof(ArtObject),
        ["Text Storage"] = typeof(TextStorage),
        ["Font"] = typeof(FezFont),
        ["Level"] = typeof(Level),
        ["Map"] = typeof(MapTree),
        ["NPC Metadata"] = typeof(NpcMetadata),
        ["Sky"] = typeof(Sky),
        ["Song"] = typeof(TrackedSong),
        ["Trile Set"] = typeof(TrileSet)
    };

    private EditorComponent CreateEditorFor(object asset, string path)
    {
        return asset switch
        {
            TrackedSong song => new DiezEditor(_game, path, song),
            TextStorage text => new PoEditor(_game, path, text),
            FezFont font => new ZuEditor(_game, path, font),
            SaveData saveData => new SallyEditor(_game, path, saveData),
            ArtObject ao => new ChrisEditor(_game, path, ao),
            TrileSet ts => new ChrisEditor(_game, path, ts),
            MapTree tree => new JadeEditor(_game, path, tree),
            Level level => new EddyEditor(_game, path, level),
            RSoundEffect soundEffect => new RickViewer(_game, path, soundEffect),
            VorbisSoundContainer oggContainer => new RickViewer(_game, path, oggContainer),
            Sky sky => new LukeEditor(_game, path, sky),
            RTexture2D texture => new TexViewer(_game, path, texture),
            RAnimatedTexture animatedTexture => new TexViewer(_game, path, animatedTexture),
            NpcMetadata npc => new MuEditor(_game, path, npc),
            _ => new NotSupportedComponent(_game, path, asset.GetType())
        };
    }

    public static IEnumerable<KeyValuePair<string, Type>> GetAssetTypes()
    {
        return AssetTypes;
    }

    public static string GetExtensionForType(Type assetType)
    {
        if (assetType == typeof(TrackedSong)) return "fezsong.json";
        if (assetType == typeof(TextStorage)) return "feztxt.json";
        if (assetType == typeof(FezFont)) return "fezfont.json";
        if (assetType == typeof(ArtObject)) return "fezao.glb";
        if (assetType == typeof(TrileSet)) return "fezts.glb";
        if (assetType == typeof(MapTree)) return "fezmap.json";
        if (assetType == typeof(Level)) return "fezlvl.json";
        if (assetType == typeof(Sky)) return "fezsky.json";
        if (assetType == typeof(NpcMetadata)) return "feznpc.json";
        throw new InvalidOperationException();
    }

    public void CreateAndSaveAsset(Type assetType, string relativePath, string defaultName)
    {
        if (assetType == typeof(Level))
        {
            _resourceService.RequestAssetPathFromUser(
                title: "Select Trile Set",
                text: "Pick trile set to use by a new level:",
                rootPath: "Trile Sets/",
                onProvided: trileSetPath =>
                {
                    var trileSet = (TrileSet)_resourceService.Load(trileSetPath);
                    _resourceService.Save(relativePath, EddyEditor.Create(defaultName, trileSet));
                });
            return;
        }

        if (assetType == typeof(MapTree))
        {
            var mapTree = new MapTree();
            var generator = new MapTreeGenerator(_game, mapTree);
            generator.Disposed += (_, _) => _resourceService.Save(relativePath, mapTree);
            _game.Components.Add(generator);
            return;
        }

        object? asset = null;
        if (assetType == typeof(TrackedSong)) asset = DiezEditor.Create(defaultName);
        if (assetType == typeof(TextStorage)) asset = PoEditor.Create();
        if (assetType == typeof(FezFont)) asset = ZuEditor.Create();
        if (assetType == typeof(ArtObject)) asset = ChrisEditor.CreateAo(defaultName);
        if (assetType == typeof(TrileSet)) asset = ChrisEditor.CreateTs(defaultName);
        if (assetType == typeof(Sky)) asset = LukeEditor.Create(defaultName);
        if (assetType == typeof(NpcMetadata)) asset = MuEditor.Create();
        if (asset == null) throw new InvalidOperationException();

        _resourceService.Save(relativePath, asset);
    }
}