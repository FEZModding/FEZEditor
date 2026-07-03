using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.MapTree;

namespace FezEditor.Tools;

public static class MapTreeExtensions
{
    public static float GetSizeFactor(this LevelNodeType nodeType)
    {
        return nodeType switch
        {
            LevelNodeType.Hub => 2f,
            LevelNodeType.Lesser => 0.5f,
            LevelNodeType.Node => 1f,
            _ => throw new InvalidOperationException()
        };
    }

    public static IEnumerable<MapNode> EnumerateNodes(this MapNode root)
    {
        var visited = new HashSet<MapNode>();
        var stack = new Stack<MapNode>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!visited.Add(node))
            {
                continue;
            }

            yield return node;

            foreach (var c in node.Connections)
            {
                stack.Push(c.Node);
            }
        }
    }

    public static (MapNode Parent, MapNodeConnection Connection)? FindParentWithConnection(this MapTree tree, MapNode node)
    {
        foreach (var parentNode in tree.Root.EnumerateNodes())
        {
            var connection = parentNode.Connections.FirstOrDefault(c => c.Node == node);
            if (connection != null)
            {
                return (parentNode, connection);
            }
        }

        return null;
    }
}