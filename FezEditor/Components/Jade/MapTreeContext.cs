using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.MapTree;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Jade;

public class MapTreeContext : IDisposable
{
    private const float LinkThickness = 0.05375f;

    public MapNode? SelectedNode { get; set; }

    private readonly Dictionary<MapNode, NodeActors> _nodeActors = new();

    private readonly JadeEditor _jade;

    private readonly MapTree _mapTree;

    private readonly Scene _scene;

    private readonly Camera _camera;

    public MapTreeContext(JadeEditor jade, MapTree mapTree, Scene scene, Camera camera)
    {
        _jade = jade;
        _mapTree = mapTree;
        _scene = scene;
        _camera = camera;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        TeardownVisualization();
    }

    public bool TryGetMeshActor(MapNode node, out Actor actor)
    {
        if (_nodeActors.TryGetValue(node, out var actors))
        {
            actor = actors.Mesh;
            return true;
        }

        actor = null!;
        return false;
    }

    public MapNode? FindNodeByActor(Actor actor)
    {
        foreach (var (node, actors) in _nodeActors)
        {
            if (actors.Mesh == actor)
            {
                return node;
            }
        }

        return null;
    }

    public void FullVisualize()
    {
        TeardownVisualization();
        BuildLayout(_mapTree.Root, null, Vector3.Zero, out var layout, out var faceOverrides);
        foreach (var (node, offset) in layout)
        {
            CreateNodeMesh(node, offset);
            CreateNodeLinks(node, offset, faceOverrides);
        }
    }

    public void PartialRevisualize()
    {
        // Destroy all link actors - links always change when layout changes.
        foreach (var (node, actors) in _nodeActors)
        {
            foreach (var link in actors.Links)
            {
                _scene.DestroyActor(link);
            }

            _nodeActors[node] = actors with { Links = new List<Actor>() };
        }

        BuildLayout(_mapTree.Root, null, Vector3.Zero, out var layout, out var faceOverrides);

        // Destroy actors for nodes no longer in tree.
        foreach (var node in _nodeActors.Keys.ToList())
        {
            if (!layout.ContainsKey(node))
            {
                _scene.DestroyActor(_nodeActors[node].Mesh);
                _scene.DestroyActor(_nodeActors[node].Icons);
                _nodeActors.Remove(node);
            }
        }

        // Update existing or create new mesh actors.
        foreach (var (node, offset) in layout)
        {
            if (_nodeActors.TryGetValue(node, out var existing))
            {
                existing.Mesh.Transform.Position = offset;
            }
            else
            {
                CreateNodeMesh(node, offset);
            }
        }

        // Recreate all link actors.
        foreach (var (node, offset) in layout)
        {
            CreateNodeLinks(node, offset, faceOverrides);
        }
    }

    private void TeardownVisualization()
    {
        foreach (var actors in _nodeActors.Values)
        {
            foreach (var link in actors.Links)
            {
                _scene.DestroyActor(link);
            }

            _scene.DestroyActor(actors.Mesh);
            _scene.DestroyActor(actors.Icons);
        }

        _nodeActors.Clear();
    }

    private static void BuildLayout(
        MapNode root,
        MapNodeConnection? rootParentConnection,
        Vector3 rootOffset,
        out Dictionary<MapNode, Vector3> layout,
        out Dictionary<MapNodeConnection, FaceOrientation> faceOverrides)
    {
        layout = new Dictionary<MapNode, Vector3>();
        faceOverrides = new Dictionary<MapNodeConnection, FaceOrientation>();
        var overrides = faceOverrides;
        var multiBranchIds = new Dictionary<MapNodeConnection, int>();
        var multiBranchCounts = new Dictionary<MapNodeConnection, int>();

        var stack = new Stack<NodeProcessingState>();
        stack.Push(new NodeProcessingState(root, rootParentConnection, rootOffset));

        while (stack.Count > 0)
        {
            var (node, parentConnection, offset) = stack.Pop();
            layout[node] = offset;

            // Resolve face conflicts for lesser nodes - store overrides locally, never write back to connection face
            foreach (var c in node.Connections)
            {
                var face = overrides.GetValueOrDefault(c, c.Face);
                if (c.Node.NodeType == LevelNodeType.Lesser &&
                    node.Connections.Any(x => overrides.GetValueOrDefault(x, x.Face) == face &&
                                              c.Node.NodeType != LevelNodeType.Lesser))
                {
                    if (node.Connections.All(x => overrides.GetValueOrDefault(x, x.Face) != FaceOrientation.Top))
                    {
                        overrides[c] = FaceOrientation.Top;
                    }
                    else if (node.Connections.All(x => overrides.GetValueOrDefault(x, x.Face) != FaceOrientation.Down))
                    {
                        overrides[c] = FaceOrientation.Down;
                    }
                }
            }

            foreach (var c in node.Connections)
            {
                multiBranchIds.TryAdd(c, 0);
            }

            foreach (var c in node.Connections)
            {
                var cFace = overrides.GetValueOrDefault(c, c.Face);
                multiBranchIds[c] = node.Connections
                    .Where(x => overrides.GetValueOrDefault(x, x.Face) == cFace)
                    .Max(x => multiBranchIds[x]) + 1;
                multiBranchCounts[c] = node.Connections.Count(x => overrides.GetValueOrDefault(x, x.Face) == cFace);
            }

            var orderedConnections = node.Connections.OrderByDescending(x => x.Node.NodeType.GetSizeFactor());
            foreach (var item in orderedConnections)
            {
                var itemFace = overrides.GetValueOrDefault(item, item.Face);
                var parentFace = parentConnection != null
                    ? overrides.GetValueOrDefault(parentConnection, parentConnection.Face)
                    : (FaceOrientation?)null;

                if (parentFace.HasValue && itemFace == parentFace.Value.GetOpposite())
                {
                    itemFace = itemFace.GetOpposite();
                    overrides[item] = itemFace;
                }

                var sizeFactor = 3f + ((node.NodeType.GetSizeFactor() + item.Node.NodeType.GetSizeFactor()) / 2f);
                if ((node.NodeType == LevelNodeType.Hub || item.Node.NodeType == LevelNodeType.Hub) &&
                    node.NodeType != LevelNodeType.Lesser && item.Node.NodeType != LevelNodeType.Lesser)
                {
                    sizeFactor += 1f;
                }

                if ((node.NodeType == LevelNodeType.Lesser || item.Node.NodeType == LevelNodeType.Lesser) &&
                    multiBranchCounts[item] == 1)
                {
                    sizeFactor -= itemFace.IsSide() ? 1 : 2;
                }

                sizeFactor *= 1.25f + item.BranchOversize;
                var num4 = sizeFactor * 0.375f;
                if (item.Node.NodeType == LevelNodeType.Node && node.NodeType == LevelNodeType.Node)
                {
                    num4 *= 1.5f;
                }

                var faceVector = itemFace.AsVector();
                var vector2 = Vector3.Zero;
                if (multiBranchCounts[item] > 1)
                {
                    vector2 = (multiBranchIds[item] - 1 - ((multiBranchCounts[item] - 1) / 2f)) *
                              (Mathz.XzMask - itemFace.AsVector().Abs()) * num4;
                }

                var childOffset = offset + (faceVector * sizeFactor) + vector2;
                stack.Push(new NodeProcessingState(item.Node, item, childOffset));
            }
        }
    }

    private void CreateNodeLinks(MapNode node, Vector3 offset, Dictionary<MapNodeConnection, FaceOrientation> faceOverrides)
    {
        var multiBranchIds = new Dictionary<MapNodeConnection, int>();
        var multiBranchCounts = new Dictionary<MapNodeConnection, int>();

        foreach (var c in node.Connections)
        {
            multiBranchIds.TryAdd(c, 0);
        }

        foreach (var c in node.Connections)
        {
            var cFace = faceOverrides.GetValueOrDefault(c, c.Face);
            multiBranchIds[c] = node.Connections
                .Where(x => faceOverrides.GetValueOrDefault(x, x.Face) == cFace)
                .Max(x => multiBranchIds[x]) + 1;
            multiBranchCounts[c] = node.Connections.Count(x => faceOverrides.GetValueOrDefault(x, x.Face) == cFace);
        }

        var num = 0f;
        var orderedConnections = node.Connections.OrderByDescending(x => x.Node.NodeType.GetSizeFactor());
        foreach (var item in orderedConnections)
        {
            var itemFace = faceOverrides.GetValueOrDefault(item, item.Face);

            var sizeFactor = 3f + ((node.NodeType.GetSizeFactor() + item.Node.NodeType.GetSizeFactor()) / 2f);
            if ((node.NodeType == LevelNodeType.Hub || item.Node.NodeType == LevelNodeType.Hub) &&
                node.NodeType != LevelNodeType.Lesser && item.Node.NodeType != LevelNodeType.Lesser)
            {
                sizeFactor += 1f;
            }

            if ((node.NodeType == LevelNodeType.Lesser || item.Node.NodeType == LevelNodeType.Lesser) &&
                multiBranchCounts[item] == 1)
            {
                sizeFactor -= itemFace.IsSide() ? 1 : 2;
            }

            sizeFactor *= 1.25f + item.BranchOversize;
            var num4 = sizeFactor * 0.375f;
            if (item.Node.NodeType == LevelNodeType.Node && node.NodeType == LevelNodeType.Node)
            {
                num4 *= 1.5f;
            }

            var faceVector = itemFace.AsVector();
            var vector2 = Vector3.Zero;
            if (multiBranchCounts[item] > 1)
            {
                vector2 = (multiBranchIds[item] - 1 - ((multiBranchCounts[item] - 1) / 2f)) *
                          (Mathz.XzMask - itemFace.AsVector().Abs()) * num4;
            }

            if (multiBranchCounts[item] > 1)
            {
                num = Math.Max(num, sizeFactor / 2f);
                var scale = (faceVector * num) + (Vector3.One * LinkThickness);
                var position = (faceVector * num / 2f) + offset;
                AppendLink(node, position, scale);

                scale = vector2 + (Vector3.One * LinkThickness);
                position = (vector2 / 2f) + offset + (faceVector * num);
                AppendLink(node, position, scale);

                var num5 = sizeFactor - num;
                scale = (faceVector * num5) + (Vector3.One * LinkThickness);
                position = (faceVector * num5 / 2f) + offset + (faceVector * num) + vector2;
                AppendLink(node, position, scale);
            }
            else
            {
                var scale = (faceVector * sizeFactor) + (Vector3.One * LinkThickness);
                var position = (faceVector * sizeFactor / 2f) + offset;
                AppendLink(node, position, scale);
            }

            switch (item.Node.LevelName)
            {
                case "LIGHTHOUSE_SPIN":
                    {
                        const float num6 = 3.425f;
                        var scale = (Vector3.Backward * num6) + (Vector3.One * LinkThickness);
                        var position = (Vector3.Backward * num6 / 2f) + offset + (faceVector * sizeFactor);
                        AppendLink(node, position, scale);
                        break;
                    }

                case "LIGHTHOUSE_HOUSE_A":
                    {
                        const float num7 = 5f;
                        var scale = (Vector3.Right * num7) + (Vector3.One * LinkThickness);
                        var position = (Vector3.Right * num7 / 2f) + offset + (faceVector * sizeFactor);
                        AppendLink(node, position, scale);
                        break;
                    }
            }
        }
    }

    private void CreateNodeMesh(MapNode node, Vector3 offset)
    {
        var mesh = _scene.CreateActor();
        mesh.Transform.Position = offset;
        mesh.Name = node.LevelName;

        var visual = mesh.AddComponent<MapNodeMesh>();
        visual.Camera = _camera;
        visual.Visualize(node);

        var icons = _scene.CreateActor(mesh);
        icons.Name = $"{node.LevelName} ^ Icons";
        icons.AddComponent<MapIconsMesh>().Visualize(node);

        _nodeActors[node] = new NodeActors(mesh, icons, new List<Actor>());
    }

    private void AppendLink(MapNode node, Vector3 position, Vector3 scale)
    {
        if (!_nodeActors.TryGetValue(node, out var actors))
        {
            return;
        }

        var actor = _scene.CreateActor();
        actor.Transform.Position = position;
        actor.Transform.Scale = scale;
        actor.Name = $"{node.LevelName} ^ Link";
        actor.AddComponent<MapLinkMesh>();
        actors.Links.Add(actor);
    }

    public void DrawProperties()
    {
        if (SelectedNode == null)
        {
            ImGui.TextDisabled("Select a node to edit its properties.");
            return;
        }

        ImGui.SeparatorText("Node");

        var levelName = SelectedNode.LevelName;
        if (ImGui.InputText("Level Name", ref levelName, 255))
        {
            using (_jade.History.BeginScope("Edit Level Name"))
            {
                SelectedNode.LevelName = levelName;
            }
        }

        var nodeType = (int)SelectedNode.NodeType;
        var nodeTypes = Enum.GetNames<LevelNodeType>();
        if (ImGui.Combo("Node Type", ref nodeType, nodeTypes, nodeTypes.Length))
        {
            using (_jade.History.BeginScope("Edit Node Type"))
            {
                SelectedNode.NodeType = (LevelNodeType)nodeType;
            }
        }

        var parent = _mapTree.FindParentWithConnection(SelectedNode);
        if (parent is { Connection: not null })
        {
            var faceNames = Enum.GetNames<FaceOrientation>();
            var faceIndex = (int)parent.Value.Connection.Face;
            if (ImGui.Combo("Parent Face", ref faceIndex, faceNames, faceNames.Length))
            {
                using (_jade.History.BeginScope("Edit Parent Face"))
                {
                    parent.Value.Connection.Face = (FaceOrientation)faceIndex;
                }
            }
        }

        ImGui.SeparatorText("Gates");

        var hasLesserGate = SelectedNode.HasLesserGate;
        if (ImGui.Checkbox("Has Lesser Gate", ref hasLesserGate))
        {
            using (_jade.History.BeginScope("Has Lesser Gate"))
            {
                SelectedNode.HasLesserGate = hasLesserGate;
            }
        }

        var hasWarpGate = SelectedNode.HasWarpGate;
        if (ImGui.Checkbox("Has Warp Gate", ref hasWarpGate))
        {
            using (_jade.History.BeginScope("Has Warp Gate"))
            {
                SelectedNode.HasWarpGate = hasWarpGate;
            }
        }

        ImGui.SeparatorText("Win Conditions");

        var chestCount = SelectedNode.Conditions.ChestCount;
        if (ImGui.InputInt("Chest Count", ref chestCount))
        {
            using (_jade.History.BeginScope("Edit Chest Count"))
            {
                SelectedNode.Conditions.ChestCount = chestCount;
            }
        }

        var lockedDoorCount = SelectedNode.Conditions.LockedDoorCount;
        if (ImGui.InputInt("Locked Door Count", ref lockedDoorCount))
        {
            using (_jade.History.BeginScope("Edit Locked Door Count"))
            {
                SelectedNode.Conditions.LockedDoorCount = lockedDoorCount;
            }
        }

        var unlockedDoorCount = SelectedNode.Conditions.UnlockedDoorCount;
        if (ImGui.InputInt("Unlocked Door Count", ref unlockedDoorCount))
        {
            using (_jade.History.BeginScope("Edit Unlocked Door Count"))
            {
                SelectedNode.Conditions.UnlockedDoorCount = unlockedDoorCount;
            }
        }

        var cubeShardCount = SelectedNode.Conditions.CubeShardCount;
        if (ImGui.InputInt("Cube Shard Count", ref cubeShardCount))
        {
            using (_jade.History.BeginScope("Edit Cube Shard Count"))
            {
                SelectedNode.Conditions.CubeShardCount = cubeShardCount;
            }
        }

        var otherCollectibleCount = SelectedNode.Conditions.OtherCollectibleCount;
        if (ImGui.InputInt("Other Collectible Count", ref otherCollectibleCount))
        {
            using (_jade.History.BeginScope("Edit Other Collectible Count"))
            {
                SelectedNode.Conditions.OtherCollectibleCount = otherCollectibleCount;
            }
        }

        var splitUpCount = SelectedNode.Conditions.SplitUpCount;
        if (ImGui.InputInt("Split Up Count", ref splitUpCount))
        {
            using (_jade.History.BeginScope("Edit Split Up Count"))
            {
                SelectedNode.Conditions.SplitUpCount = splitUpCount;
            }
        }

        var secretCount = SelectedNode.Conditions.SecretCount;
        if (ImGui.InputInt("Secret Count", ref secretCount))
        {
            using (_jade.History.BeginScope("Edit Secret Count"))
            {
                SelectedNode.Conditions.SecretCount = secretCount;
            }
        }

        var scriptIds = new Dirty<List<int>>(SelectedNode.Conditions.ScriptIds);
        if (ImGuiX.EditableList("Script Ids", ref scriptIds, RenderInt, () => 0))
        {
            using (_jade.History.BeginScope("Edit Script Ids"))
            {
                SelectedNode.Conditions.ScriptIds = scriptIds;
            }
        }

        if (SelectedNode.Connections.Count > 0)
        {
            ImGui.SeparatorText("Connection Branch Oversizes");

            foreach (var connection in SelectedNode.Connections)
            {
                var branchOversize = connection.BranchOversize;
                if (ImGui.InputFloat(connection.Node.LevelName, ref branchOversize))
                {
                    using (_jade.History.BeginScope("Edit Branch Oversize"))
                    {
                        connection.BranchOversize = branchOversize;
                        break;
                    }
                }
            }
        }
    }

    private static bool RenderInt(int index, ref int item)
    {
        return ImGui.InputInt("##item", ref item);
    }

    private record NodeActors(Actor Mesh, Actor Icons, List<Actor> Links);

    private record struct NodeProcessingState(
        MapNode Node,
        MapNodeConnection? ParentConnection,
        Vector3 Offset
    );
}