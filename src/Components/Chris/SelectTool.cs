using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

internal class SelectTool : BaseTool
{
    private static readonly Color HoverColor = Color.Blue with { A = 85 };

    private static readonly Color SelectionColor = Color.Red with { A = 85 };

    private TrixelFace? _dragStartFace;

    private TrixelFace? _lastHover;

    private readonly HashSet<TrixelFace> _lastSelection = new();

    private bool _fullFaceSelectionActive;

    public SelectTool(Game game, IChrisEditor chris) : base(game, chris)
    {
    }

    protected override void TestConditions()
    {
        if (Chris.CurrentTool == ChrisTool.Look)
        {
            _lastHover = null;
            _lastSelection.Clear();
            Chris.Cursor.Clear();
            return;
        }

        if (Chris.SelectedFaces.Count != 0)
        {
            _lastHover = null;
            if (!_lastSelection.SetEquals(Chris.SelectedFaces))
            {
                _lastSelection.Clear();
                _lastSelection.UnionWith(Chris.SelectedFaces);
                Chris.Cursor.SetFaces(Chris.SelectedFaces, Chris.Obj.Offset, SelectionColor);
            }
            return;
        }

        _lastSelection.Clear();
        if (Chris.Hit != _lastHover)
        {
            _lastHover = Chris.Hit;
            if (Chris.Hit.HasValue)
            {
                Chris.Cursor.SetFaces([Chris.Hit.Value], Chris.Obj.Offset, HoverColor);
            }
            else
            {
                Chris.Cursor.Clear();
            }
        }
    }

    protected override void Act()
    {
        if (!Chris.IsViewportHovered ||
            Chris.CurrentTool is not (ChrisTool.Add or ChrisTool.Remove or ChrisTool.Bucket))
        {
            return;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _dragStartFace = Chris.Hit;
            if (!Chris.Hit.HasValue)
            {
                Chris.SelectedFaces.Clear();
                return;
            }
        }

        if (ImGui.GetIO().KeyShift && Chris.Hit.HasValue)
        {
            _fullFaceSelectionActive = true;
            if (!Chris.SelectedFaces.Contains(Chris.Hit.Value))
            {
                ApplySelection(Chris.Obj.FloodFillFaces(Chris.Hit.Value, _ => true));
            }
        }
        else if (_fullFaceSelectionActive)
        {
            _fullFaceSelectionActive = false;
            Chris.SelectedFaces.Clear();
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            _dragStartFace = null;
        }
        else if (Chris.Hit.HasValue && _dragStartFace.HasValue && !_fullFaceSelectionActive)
        {
            var min = Vector3I.Min(_dragStartFace.Value.Emplacement, Chris.Hit.Value.Emplacement);
            var max = Vector3I.Max(_dragStartFace.Value.Emplacement, Chris.Hit.Value.Emplacement);
            var result = new HashSet<TrixelFace>();

            foreach (var tf in Chris.Obj.VisibleFaces)
            {
                if ((tf.Face == _dragStartFace.Value.Face || tf.Face == Chris.Hit.Value.Face) &&
                    tf.Emplacement >= min &&
                    tf.Emplacement <= max)
                {
                    result.Add(tf);
                }
            }

            ApplySelection(result);
        }
    }

    private void ApplySelection(HashSet<TrixelFace> result)
    {
        if (!result.SetEquals(Chris.SelectedFaces))
        {
            Chris.SelectedFaces.Clear();
            Chris.SelectedFaces.UnionWith(result);
        }
    }

    protected override bool IsToolAllowed(ChrisTool tool)
    {
        return true;
    }
}