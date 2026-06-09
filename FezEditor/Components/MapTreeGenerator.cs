using FezEditor.Services;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.MapTree;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Serilog;

namespace FezEditor.Components;

public class MapTreeGenerator : DrawableGameComponent
{
    private static readonly ILogger Logger = Logging.Create<MapTreeGenerator>();

    private static readonly string[] UpLevels = new[]
    {
        "TREE_ROOTS", "TREE", "TREE_SKY", "FOX",
        "WATER_TOWER", "PIVOT_WATERTOWER", "VILLAGEVILLE_3D"
    };

    private static readonly string[] DownLevels = new[]
    {
        "SEWER_START", "MEMORY_CORE", "ZU_FORK", "STARGATE", "QUANTUM"
    };

    private static readonly string[] OppositeLevels = new[]
    {
        "NUZU_SCHOOL", "NUZU_ABANDONED_A", "ZU_HOUSE_EMPTY_B", "PURPLE_LODGE",
        "ZU_HOUSE_SCAFFOLDING", "MINE_BOMB_PILLAR", "CMY_B", "INDUSTRIAL_HUB",
        "SUPERSPIN_CAVE", "GRAVE_LESSER_GATE", "THRONE", "VISITOR",
        "ORRERY", "LAVA_SKULL", "LAVA_FORK"
    };

    private static readonly string[] BackLevels = new[] { "ABANDONED_B", "LAVA" };

    private static readonly string[] FrontLevels = new[] { "VILLAGEVILLE_3D", "ZU_LIBRARY" };

    private static readonly string[] RightLevels = new[]
    {
        "WALL_SCHOOL", "WALL_KITCHEN", "WALL_INTERIOR_HOLE", "WALL_INTERIOR_B", "WALL_INTERIOR_A"
    };

    private static readonly string[] PuzzleLevels = new[]
    {
        "ZU_ZUISH", "ZU_UNFOLD", "BELL_TOWER", "CLOCK", "ZU_TETRIS"
    };

    private static readonly string[] GateArtObjects = new[]
    {
        "GATE_GRAVEAO", "GATEAO", "GATE_INDUSTRIALAO",
        "GATE_SEWERAO", "ZU_GATEAO", "GRAVE_GATEAO"
    };

    private static readonly Dictionary<string, float> OversizeLinks = new()
    {
        ["SEWER_START"] = 5.5f,
        ["TREE"] = 1.25f,
        ["TREE_SKY"] = 1f,
        ["INDUSTRIAL_HUB"] = 0.5f,
        ["VILLAGEVILLE_3D"] = -0.5f,
        ["WALL_VILLAGE"] = 0.5f,
        ["ZU_CITY"] = 0.5f,
        ["INDUSTRIAL_CITY"] = 0.5f,
        ["MEMORY_CORE"] = 0.5f,
        ["BIG_TOWER"] = 0.5f,
        ["STARGATE"] = -0.5f,
        ["WATERFALL"] = 0.25f,
        ["BELL_TOWER"] = 0.25f,
        ["LIGHTHOUSE"] = 0.25f,
        ["ARCH"] = 0.25f
    };

    private readonly ResourceService _resources;

    private string _rootLevelName = "";

    private readonly MapTree _tree;

    private float _progress;

    private string _status = "";

    private State _state = State.SelectingRoot;

    private State _previousState = State.Disposed;

    private CancellationTokenSource? _cts;

    public MapTreeGenerator(Game game, MapTree tree) : base(game)
    {
        _resources = game.GetService<ResourceService>();
        _tree = tree;
        _resources.RequestAssetPathFromUser(
            title: "Select Root Level",
            text: "Pick the root level to generate the map tree from:",
            rootPath: "Levels/",
            onProvided: levelPath =>
            {
                _rootLevelName = Path.GetFileName(levelPath).ToUpperInvariant();
                _ = ProcessAsync();
            });
    }

    public override void Update(GameTime gameTime)
    {
        if (_state == State.Disposed)
        {
            Game.RemoveComponent(this);
        }
    }

    public override void Draw(GameTime gameTime)
    {
        if (_state is State.Disposed or State.SelectingRoot)
        {
            return;
        }

        const string popup = "Map Tree##Generator";
        if (_state != _previousState)
        {
            ImGui.OpenPopup(popup);
            _previousState = _state;
        }

        var isOpen = true;
        ImGuiX.SetNextWindowCentered(ImGuiCond.Always);
        if (ImGui.BeginPopupModal(popup, ref isOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse))
        {
            switch (_state)
            {
                case State.Processing:
                    ImGui.Text(_status);
                    ImGuiX.ProgressBar(_progress, new Vector2(400, 0), $"{_progress * 100:F1}%");
                    if (ImGui.Button("Cancel"))
                    {
                        _cts?.Cancel();
                        ImGui.CloseCurrentPopup();
                    }

                    break;

                case State.Complete:
                    _state = State.Disposed;
                    ImGui.CloseCurrentPopup();
                    break;
            }

            ImGui.EndPopup();
        }

        if (!isOpen)
        {
            _cts?.Cancel();
            _state = State.Disposed;
        }
    }

    private async Task ProcessAsync()
    {
        _cts = new CancellationTokenSource();
        _state = State.Processing;
        _status = "Generating map tree...";
        _progress = 0f;

        try
        {
            var ct = _cts.Token;
            await Task.Run(() => ProcessInternal(ct), ct);
            _status = "Generation complete!";
            _progress = 1.0f;
        }
        catch (OperationCanceledException)
        {
            Logger.Information("Map tree generation cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Map tree generation failed");
        }
        finally
        {
            _state = State.Complete;
        }
    }

    private void ProcessInternal(CancellationToken ct)
    {
        var root = new MapNode { LevelName = _rootLevelName };
        _tree.Root = root;

        var visited = new Dictionary<string, MapNode>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(MapNode Node, MapNode? Parent, FaceOrientation Origin)>();

        visited[root.LevelName] = root;
        queue.Enqueue((root, null, FaceOrientation.Front));

        var processed = 0;
        var discovered = 1;
        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var (node, parentNode, origin) = queue.Dequeue();
            processed++;
            _status = $"Processing {node.LevelName}...";
            _progress = (float)processed / discovered;

            Level level;
            TrileSet trileSet;
            try
            {
                level = _resources.Load<Level>("Levels/" + node.LevelName);
                trileSet = _resources.Load<TrileSet>("Trile Sets/" + level.TrileSetName);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Skipping level {0}", node.LevelName);
                continue;
            }

            FillNodeProperties(node, level, trileSet);

            foreach (var script in level.Scripts.Values)
            {
                if (script.Actions == null)
                {
                    continue;
                }

                foreach (var action in script.Actions)
                {
                    if (action.Object.Type != "Level" || !action.Operation.Contains("Level"))
                    {
                        continue;
                    }

                    var connection = new MapNodeConnection();
                    var hasConnection = true;

                    var trigger = script.Triggers.EmptyIfNull()
                        .Where(st => st.Object.Type == "Volume")
                        .Where(st => st.Object.Identifier.HasValue)
                        .FirstOrDefault(st => st.Event == "Enter");

                    if (trigger != null)
                    {
                        var volumeId = trigger.Object.Identifier!.Value;
                        if (!level.Volumes.TryGetValue(volumeId, out var volume))
                        {
                            Logger.Warning(
                                "A level-changing script links to a non-existent volume in {0} (Volume ID = {1})",
                                level.Name, volumeId);
                            hasConnection = false;
                        }
                        else if (volume.ActorSettings is { IsSecretPassage: true })
                        {
                            hasConnection = false;
                        }
                        else
                        {
                            connection.Face = volume.Orientations.First();
                        }
                    }

                    if (!hasConnection)
                    {
                        continue;
                    }

                    var lastLevel = action.Operation == "ReturnToLastLevel"
                        ? parentNode?.LevelName ?? ""
                        : action.Arguments[0];

                    switch (lastLevel)
                    {
                        case "THRONE":
                            if (level.Name == "ZU_CITY_RUINS")
                            {
                                continue;
                            }

                            break;
                        case "PYRAMID":
                        case "CABIN_INTERIOR_A":
                            continue;
                    }

                    if (lastLevel == "ZU_CITY_RUINS" && level.Name == "THRONE")
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(lastLevel))
                    {
                        continue;
                    }

                    if (visited.TryGetValue(lastLevel, out _))
                    {
                        break;
                    }

                    var childNode = new MapNode { LevelName = lastLevel };
                    visited[lastLevel] = childNode;

                    connection.Node = childNode;
                    if (childNode != parentNode)
                    {
                        if (parentNode != null && origin == connection.Face)
                        {
                            connection.Face = origin.GetOpposite();
                        }

                        if (UpLevels.Contains(lastLevel))
                        {
                            connection.Face = FaceOrientation.Top;
                        }
                        else if (DownLevels.Contains(lastLevel))
                        {
                            connection.Face = FaceOrientation.Down;
                        }
                        else if (OppositeLevels.Contains(lastLevel))
                        {
                            connection.Face = connection.Face.GetOpposite();
                        }
                        else if (BackLevels.Contains(lastLevel))
                        {
                            connection.Face = FaceOrientation.Back;
                        }
                        else if (RightLevels.Contains(lastLevel))
                        {
                            connection.Face = FaceOrientation.Right;
                        }
                        else if (FrontLevels.Contains(lastLevel))
                        {
                            connection.Face = FaceOrientation.Front;
                        }

                        connection.BranchOversize = OversizeLinks.GetValueOrDefault(lastLevel, 0f);
                        node.Connections.Add(connection);
                        queue.Enqueue((childNode, node, connection.Face));
                        discovered++;
                    }

                    break;
                }
            }
        }
    }

    private static void FillNodeProperties(MapNode node, Level level, TrileSet trileSet)
    {
        node.NodeType = level.NodeType;

        node.HasLesserGate = level.ArtObjects.Values
            .Where(aoi => aoi.Name.Contains("lesser_gate", StringComparison.OrdinalIgnoreCase))
            .Any(aoi => !aoi.Name.Contains("base", StringComparison.OrdinalIgnoreCase));

        node.HasWarpGate = level.ArtObjects.Values
            .Any(aoi => GateArtObjects.Contains(aoi.Name));

        node.Conditions.ChestCount = level.ArtObjects.Values
            .Count(aoi => aoi.Name.Contains("treasure", StringComparison.OrdinalIgnoreCase)) / 2;

        node.Conditions.ScriptIds = level.Scripts
            .Where(kv => kv.Value.IsWinCondition)
            .Select(kv => kv.Key)
            .ToList();

        node.Conditions.SplitUpCount = level.Triles.Values
            .Union(level.Triles.Values.SelectMany(ti => ti.OverlappedTriles.EmptyIfNull()))
            .Count(ti => GetTrileType(ti) == ActorType.GoldenCube);

        node.Conditions.CubeShardCount = level.Triles.Values
            .Count(ti => GetTrileType(ti) == ActorType.CubeShard);

        node.Conditions.OtherCollectibleCount = level.Triles.Values
            .Count(ti => IsTreasure(GetTrileType(ti)));

        node.Conditions.OtherCollectibleCount += level.ArtObjects.Values
            .Count(aoi => aoi.Name.Equals("treasure_mapAO", StringComparison.OrdinalIgnoreCase));

        node.Conditions.LockedDoorCount = level.Triles.Values
            .Count(ti => GetTrileType(ti) == ActorType.Door);

        node.Conditions.UnlockedDoorCount = level.Triles.Values
            .Count(ti => GetTrileType(ti) == ActorType.UnlockedDoor);

        node.Conditions.SecretCount = 0;

        node.Conditions.SecretCount += level.ArtObjects
            .Count(kv => kv.Value.Name.Contains("fork", StringComparison.OrdinalIgnoreCase));

        node.Conditions.SecretCount += level.ArtObjects
            .Count(kv => kv.Value.Name.Contains("qr", StringComparison.OrdinalIgnoreCase));

        node.Conditions.SecretCount += level.Volumes
            .Count(kv => kv.Value.ActorSettings is { CodePattern.Length: > 0 });

        node.Conditions.SecretCount += level.Name != "OWL"
            ? level.NonPlayerCharacters.Count(kv => kv.Value.Name == "Owl")
            : 0;

        node.Conditions.SecretCount += level.ArtObjects
            .Where(kv => kv.Value.Name.Contains("BIT_DOOR", StringComparison.OrdinalIgnoreCase))
            .Count(kv => !kv.Value.Name.Contains("BROKEN", StringComparison.OrdinalIgnoreCase));

        node.Conditions.SecretCount += level.Scripts.Values
            .Count(s => s.Actions != null && s.Actions.Any(sa => sa.Object.Type == "Level" && sa.Operation == "ResolvePuzzle"));

        node.Conditions.SecretCount += PuzzleLevels.Contains(level.Name)
            ? level.Name != "CLOCK" ? 1 : 4
            : 0;

        return;

        ActorType GetTrileType(TrileInstance instance)
        {
            return instance.TrileId >= 0 && trileSet.Triles.TryGetValue(instance.TrileId, out var trile)
                ? trile.Type
                : ActorType.None;
        }
    }

    protected override void Dispose(bool disposing)
    {
        _cts?.Dispose();
        base.Dispose(disposing);
    }

    private static bool IsTreasure(ActorType type)
    {
        switch (type)
        {
            // case ActorType.CubeShard:
            case ActorType.SkeletonKey:
            case ActorType.NumberCube:
            case ActorType.LetterCube:
            case ActorType.TriSkull:
            case ActorType.Tome:
            case ActorType.SecretCube:
            case ActorType.TreasureMap:
            case ActorType.Mail:
            case ActorType.PieceOfHeart:
                return true;
            default:
                return false;
        }
    }

    private enum State
    {
        Disposed,
        SelectingRoot,
        Processing,
        Complete
    }
}
