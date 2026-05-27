using FezEditor.Actors;
using FezEditor.Components.Jade;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.Level.Scripting;
using FEZRepacker.Core.Definitions.Game.MapTree;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class JadeEditor : EditorComponent
{
    public override object Asset => _mapTree;

    private readonly MapTree _mapTree;

    private readonly ConfirmWindow _confirm;

    private Scene _scene = null!;

    private Actor _cameraActor = null!;

    private MapTreeContext _context = null!;

    private MapNode? _pendingRemoveNode;

    private bool _pendingRevisualize;

    private bool _showProperties = true;

    private bool _showTree;

    private string _treeFilter = "";

    public JadeEditor(Game game, string title, MapTree mapTree) : base(game, title)
    {
        _mapTree = mapTree;
        History.Track(mapTree);
        History.StateChanged += _ => _pendingRevisualize = true;
        Game.AddComponent(_confirm = new ConfirmWindow(game));
    }

    public override void LoadContent()
    {
        _scene = new Scene(Game, ContentManager);
        _scene.Lighting.Ambient = new Color(new Vector3(1f / 3f));
        Camera camera;
        {
            _cameraActor = _scene.CreateActor();
            _cameraActor.Name = "Camera";

            camera = _cameraActor.AddComponent<Camera>();
            var orbit = _cameraActor.AddComponent<OrbitControl>();
            _cameraActor.AddComponent<MapPanControl>();
            _cameraActor.AddComponent<MapZoomControl>();
            _cameraActor.AddComponent<OrientationGizmo>();

            camera.Offset = new Vector3(0, 0, 250f);
            orbit.Yaw = MathF.PI / 4f;
            orbit.Pitch = -MathF.PI / 8f;
            orbit.PitchClamp = new Vector2(-MathF.PI / 8f, MathF.PI / 8f * 3f);
        }
        {
            var actor = _scene.CreateActor();
            var stars = actor.AddComponent<StarsMesh>();
            stars.Camera = _cameraActor.GetComponent<Camera>();
        }

        _context = new MapTreeContext(this, _mapTree, _scene, camera);
        _context.FullVisualize();
    }

    public override void Update(GameTime gameTime)
    {
        if (_pendingRevisualize)
        {
            _context.PartialRevisualize();
            _pendingRevisualize = false;
            return;
        }

        StatusService.AddHints(("LMB", "Select Node"));
        _scene.Update(gameTime);
    }

    public override void Draw()
    {
        DrawToolbar();

        var size = ImGuiX.GetContentRegionAvail();
        var w = (int)size.X;
        var h = (int)size.Y;

        if (w > 0 && h > 0)
        {
            var texture = _scene.Viewport.GetTexture();
            if (texture == null || texture.Width != w || texture.Height != h)
            {
                _scene.Viewport.SetSize(w, h);
            }

            if (texture is { IsDisposed: false })
            {
                ImGuiX.Image(texture, size);
                InputService.IsViewportHovered = ImGui.IsItemHovered();

                var imageMin = ImGuiX.GetItemRectMin();
                var gizmo = _cameraActor.GetComponent<OrientationGizmo>();
                gizmo.UseFaceLabels = true;
                gizmo.Draw(imageMin + new Vector2(size.X - 8f, 8f));
                ImGuiX.DrawStats(imageMin + new Vector2(8, 8), RenderingService.GetStats());

                if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    var viewportMin = ImGuiX.GetItemRectMin();
                    var ray = _scene.Viewport.Unproject(ImGuiX.GetMousePos(), viewportMin);
                    var actor = _scene.Raycast(ray)?.Actor;
                    _context.SelectedNode = actor != null ? SelectNode(actor) : null;
                }
            }
        }

        #region Properties Window

        if (_showProperties)
        {
            const ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize |
                                           ImGuiWindowFlags.NoCollapse;
            if (ImGui.Begin($"Properties##{Title}", ref _showProperties, flags))
            {
                _context.DrawProperties();
                ImGui.End();
            }
        }

        #endregion

        #region Map Tree Window

        if (_showTree)
        {
            ImGuiX.SetNextWindowSize(new Vector2(280, 500), ImGuiCond.FirstUseEver);
            const ImGuiWindowFlags flags1 = ImGuiWindowFlags.NoCollapse;

            if (ImGui.Begin($"Map Tree##{Title}", ref _showTree, flags1))
            {
                ImGui.Text("Filter");
                ImGui.SameLine();
                ImGui.InputText($"{Lucide.Search}##treeFilter", ref _treeFilter, 255);
                ImGui.Separator();

                if (ImGui.Button($"{Lucide.Plus} Add New Node"))
                {
                    ResourceService.RequestAssetPathFromUser(
                        title: "Select Level",
                        text: "Pick a level to add as a new map node:",
                        rootPath: "Levels/",
                        onProvided: levelPath =>
                        {
                            var level = ResourceService.Load<Level>(levelPath);
                            AddMapNode(level);
                        });
                }

                ImGui.Separator();
                DrawMapTree();

                ImGui.End();
            }
        }

        #endregion

        if (_pendingRemoveNode != null)
        {
            var nodeToRemove = _pendingRemoveNode;
            _confirm.Text = $"Delete \"{nodeToRemove.LevelName}\" map node?";
            _confirm.Confirmed = () => RemoveMapNode(nodeToRemove);
            _confirm.Closed = null;
            _pendingRemoveNode = null;
        }
    }

    private void DrawToolbar()
    {
        {
            ImGui.BeginDisabled(_showTree);
            if (ImGui.Button($"{Lucide.ListTree} Structure"))
            {
                _showTree = true;
            }

            ImGui.EndDisabled();
        }

        ImGui.SameLine();
        {
            ImGui.BeginDisabled(_showProperties);
            if (ImGui.Button($"{Lucide.Wrench} Properties"))
            {
                _showProperties = true;
            }

            ImGui.EndDisabled();
        }

        ImGui.SameLine();
        {
            if (ImGui.Button($"{Lucide.GitBranchPlus} Regenerate"))
            {
                var scope = History.BeginScope("Regenerate Map Tree");
                var generator = new MapTreeGenerator(Game, _mapTree);
                generator.Disposed += (_, _) => scope.Dispose();
                Game.AddComponent(generator);
            }
        }
    }

    private bool TreeNodeMatchesFilter(MapNode node)
    {
        if (string.IsNullOrEmpty(_treeFilter))
        {
            return true;
        }

        if (node.LevelName.Contains(_treeFilter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return node.Connections.Any(c => TreeNodeMatchesFilter(c.Node));
    }

    private void DrawMapTree()
    {
        ImGui.BeginChild("##treeScroll");
        ImGui.PushStyleVar(ImGuiStyleVar.IndentSpacing, 8f);

        // Stack entries: null = TreePop sentinel, non-null = node to draw.
        var stack = new Stack<MapNode?>();
        stack.Push(_mapTree.Root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node == null)
            {
                ImGui.TreePop();
                continue;
            }

            if (!TreeNodeMatchesFilter(node))
            {
                continue;
            }

            var isSelected = node == _context.SelectedNode;
            if (isSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Header, ImGui.GetColorU32(ImGuiCol.HeaderActive));
            }

            if (!string.IsNullOrEmpty(_treeFilter))
            {
                ImGui.SetNextItemOpen(true);
            }

            var isOpen = ImGui.TreeNodeEx($"##{node.GetHashCode()}",
                ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.DefaultOpen |
                (isSelected ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None) |
                (node.Connections.Count == 0 ? ImGuiTreeNodeFlags.Leaf : ImGuiTreeNodeFlags.None));

            if (isSelected)
            {
                ImGui.PopStyleColor();
            }

            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
                _context.SelectedNode = node;
                if (_context.TryGetMeshActor(node, out var meshActor))
                {
                    _cameraActor.GetComponent<MapPanControl>().FocusOn(meshActor.Transform.Position);
                }
            }

            ImGui.SameLine();
            ImGui.Text(node.LevelName);

            if (node != _mapTree.Root)
            {
                var buttonWidth = ImGui.CalcTextSize(Lucide.X).X + ImGui.GetStyle().FramePadding.X * 2;
                var cursorX = ImGui.GetWindowWidth() - buttonWidth - ImGui.GetStyle().ScrollbarSize - 4f;
                if (cursorX > ImGui.GetCursorPosX())
                {
                    ImGui.SameLine(cursorX);
                }

                ImGuiX.PushStyleColor(ImGuiCol.Button, Color.Transparent);
                if (ImGui.Button($"{Lucide.X}##{node.GetHashCode()}"))
                {
                    _pendingRemoveNode = node;
                }

                ImGui.PopStyleColor();
            }

            if (isOpen)
            {
                // Push TreePop sentinel first so it runs after all children.
                stack.Push(null);
                // Push children in reverse order so they are drawn top-to-bottom.
                for (var i = node.Connections.Count - 1; i >= 0; i--)
                {
                    stack.Push(node.Connections[i].Node);
                }
            }
        }

        ImGui.PopStyleVar();
        ImGui.EndChild();
    }

    public override void Dispose()
    {
        _context.Dispose();
        _scene.Dispose();
        Game.RemoveComponent(_confirm);
        base.Dispose();
    }

    private MapNode SelectNode(Actor actor)
    {
        var node = _context.FindNodeByActor(actor) ?? throw new ArgumentException("Mapping for actor not found");
        var panControl = _cameraActor.GetComponent<MapPanControl>();
        panControl.FocusOn(actor.Transform.Position);
        return node;
    }

    private void AddMapNode(Level level)
    {
        var allNodes = _mapTree.Root
            .EnumerateNodes()
            .ToDictionary(mn => mn.LevelName, mn => mn, StringComparer.OrdinalIgnoreCase);

        if (allNodes.ContainsKey(level.Name))
        {
            return;
        }

        // Forward: new level's script targets an already-existing node.
        MapNode? parent = null;
        foreach (var (action, _) in GetLevelTransitions(level))
        {
            if (allNodes.TryGetValue(action.Arguments[0], out var targetNode))
            {
                parent = targetNode;
                break;
            }
        }

        // Reverse: an existing node's script targets the new level.
        if (parent == null)
        {
            foreach (var existingNode in allNodes.Values)
            {
                var existingLevel = ResourceService.Load<Level>("Levels/" + existingNode.LevelName);
                foreach (var (action, _) in GetLevelTransitions(existingLevel))
                {
                    if (string.Equals(action.Arguments[0], level.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        parent = existingNode;
                        break;
                    }
                }

                if (parent != null)
                {
                    break;
                }
            }
        }

        var face = FaceOrientation.Front;
        foreach (var (_, volume) in GetLevelTransitions(level))
        {
            if (volume.Orientations.Length > 0)
            {
                face = volume.Orientations[0];
                break;
            }
        }

        parent ??= _mapTree.Root;
        using (History.BeginScope("Add Map Node"))
        {
            var newNode = new MapNode { LevelName = level.Name };
            parent.Connections.Add(new MapNodeConnection { Node = newNode, Face = face });
        }
    }

    private static IEnumerable<(ScriptAction Action, Volume Volume)> GetLevelTransitions(Level level)
    {
        foreach (var script in level.Scripts.Values)
        {
            foreach (var action in script.Actions)
            {
                if (action.Object.Type != "Level" || !action.Operation.Contains("Level"))
                {
                    continue;
                }

                if (action.Operation == "ReturnToLastLevel" || action.Arguments.Length == 0)
                {
                    continue;
                }

                var trigger = script.Triggers
                    .Where(t => t.Object is { Type: "Volume", Identifier: not null })
                    .FirstOrDefault(t => t.Event == "Enter");

                if (trigger == null)
                {
                    continue;
                }

                if (!level.Volumes.TryGetValue(trigger.Object.Identifier!.Value, out var volume))
                {
                    continue;
                }

                yield return (action, volume);
                break;
            }
        }
    }

    private void RemoveMapNode(MapNode node)
    {
        var result = _mapTree.FindParentWithConnection(node);
        if (result == null)
        {
            return;
        }

        var (parent, connection) = result.Value;
        using (History.BeginScope("Remove Map Node"))
        {
            parent.Connections.Remove(connection);
            if (_context.SelectedNode != null && node.EnumerateNodes().Contains(_context.SelectedNode))
            {
                _context.SelectedNode = null;
            }
        }
    }
}