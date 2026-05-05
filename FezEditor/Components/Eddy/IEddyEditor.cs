using FezEditor.Actors;
using FezEditor.Structure;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal interface IEddyEditor
{
    History History { get; }

    Scene Scene { get; }

    Clock Clock { get; }

    AssetBrowser AssetBrowser { get; }

    InstanceBrowser InstanceBrowser { get; }

    Camera Camera { get; }

    CursorMesh Cursor { get; }

    Gizmo Gizmo { get; }

    bool IsViewportHovered { get; }

    Ray Ray { get; }

    RaycastHit? Hit { get; }

    EddyTool Tool { get; set; }

    HashSet<EddyTool> AllowedTools { get; }

    EddyContext HoveredContext { get; set; }

    EddyContext SelectedContext { get; set; }

    Dirty<EddyVisuals> Visuals { get; set; }

    object? Pending { get; set; }

    int OverlapIndex { get; set; }

    void FocusOn(Vector3 target);
}