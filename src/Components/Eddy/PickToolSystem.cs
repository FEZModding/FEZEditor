using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;

namespace FezEditor.Components.Eddy;

public class PickToolSystem : EddySystem
{
    private AssetEntry? _hovered;

    public override void Update()
    {
        if (Eddy.Tool is not ToolState.Pick)
        {
            _hovered = null;
            return;
        }

        Status.AddHints(("LMB", "Pick Asset"));
        if (Eddy.Hovered?.Instance is not InstanceId.Trile and not InstanceId.TrileGroup)
        {
            Status.AddHints(("Alt+LMB", "Cycle Pick"));
        }

        _hovered = Eddy.Hovered?.Instance switch
        {
            InstanceId.Trile t =>
                ResolveTrileAsset(t.Emplacement),

            InstanceId.TrileOverlap to =>
                ResolveTrileAsset(to.Emplacement),

            InstanceId.ArtObject ao =>
                new AssetEntry.ArtObject(Level.ArtObjects[ao.Id].Name),

            InstanceId.BackgroundPlane bp =>
                new AssetEntry.BackgroundPlane(Level.BackgroundPlanes[bp.Id].TextureName),

            InstanceId.NonPlayableCharacter npc =>
                new AssetEntry.NonPlayableCharacter(Level.NonPlayerCharacters[npc.Id].Name),

            _ => null
        };

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _hovered != null)
        {
            Eddy.Tool = Eddy.PickAndPaint(_hovered);
        }
    }

    public override void Draw()
    {
        if (_hovered != null)
        {
            var thumb = Eddy.Thumbnails.Get(_hovered);
            ImGuiX.DrawCursorThumbnail(thumb, $"Pick {_hovered.DisplayName}?");
        }
    }

    private AssetEntry.Trile? ResolveTrileAsset(TrileEmplacement emplacement)
    {
        var id = Eddy.GetActiveTrile(emplacement)?.TrileId ?? EddyEditor.InvalidId;
        if (id == EddyEditor.InvalidId || !Eddy.TrileSet.Triles.TryGetValue(id, out var trile))
        {
            return null;
        }

        var name = trile.Name;
        var path = Level.TrileSetName + "/" + name;
        return new AssetEntry.Trile(name, path, id);
    }
}