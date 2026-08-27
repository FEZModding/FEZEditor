using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public class PaintToolSystem : EddySystem
{
    public override void Update()
    {
        if (Eddy.Tool is not ToolState.Paint tool)
        {
            return;
        }

        if (tool is ToolState.Paint.None)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                Eddy.Tool = new ToolState.Select();
            }

            return;
        }

        if (tool is ToolState.Paint.Trile)
        {
            return;
        }

        var label = tool switch
        {
            ToolState.Paint.ArtObject => "Place Art Object",
            ToolState.Paint.BackgroundPlane => "Place Background Plane",
            ToolState.Paint.NonPlayableCharacter => "Place NPC",
            ToolState.Paint.Path => "Start a new level path",
            ToolState.Paint.Volume => "Place Volume",
            _ => throw new ArgumentOutOfRangeException(nameof(tool))
        };

        Hints.Add("LMB", label);

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Eddy.Tool = new ToolState.Select();
            return;
        }

        if (Eddy.HoveredTrile is not { } hovered || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        using (Eddy.History.BeginScope(label))
        {
            var created = Place(tool, hovered);
            if (created != null)
            {
                Eddy.Selected = created switch
                {
                    InstanceId.ArtObject or
                        InstanceId.BackgroundPlane or
                        InstanceId.NonPlayableCharacter or
                        InstanceId.Volume => new SelectionState.Instance([created]),

                    InstanceId.Path => new SelectionState.Path(created, [0]),

                    _ => Eddy.Selected
                };

                Eddy.Tool = new ToolState.Select();
            }
        }
    }

    public override void Draw()
    {
        switch (Eddy.Tool)
        {
            case ToolState.Paint.ArtObject ao:
                {
                    var thumb = Eddy.Thumbnails.Get(new AssetEntry.ArtObject(ao.AssetName));
                    ImGuiX.DrawCursorThumbnail(thumb, $"Place {ao.AssetName} here?");
                    break;
                }

            case ToolState.Paint.BackgroundPlane bp:
                {
                    var thumb = Eddy.Thumbnails.Get(new AssetEntry.BackgroundPlane(bp.AssetName));
                    ImGuiX.DrawCursorThumbnail(thumb, $"Place {bp.AssetName} here?");
                    break;
                }

            case ToolState.Paint.NonPlayableCharacter npc:
                {
                    var thumb = Eddy.Thumbnails.Get(new AssetEntry.NonPlayableCharacter(npc.AssetName));
                    ImGuiX.DrawCursorThumbnail(thumb, $"Place {npc.AssetName} here?");
                    break;
                }

            case ToolState.Paint.Volume:
                {
                    ImGuiX.DrawCursorLabel($"Place {Lucide.SquareDashed} volume here?");
                    break;
                }

            case ToolState.Paint.Path:
                {
                    ImGuiX.DrawCursorLabel($"Start {Lucide.Route} level path here?");
                    break;
                }

            case ToolState.Paint.None:
                {
                    const string text = $"Select asset from {Lucide.Sprout} Asset Browser or {Lucide.Pipette} pick it";
                    ImGuiX.DrawCursorLabel(text);
                    break;
                }
        }
    }

    private InstanceId? Place(ToolState.Paint tool, (InstanceId.Trile Trile, FaceOrientation Face) hovered)
    {
        var position = hovered.Trile.Emplacement.AsVector().ToXna() + hovered.Face.AsVector();
        switch (tool)
        {
            case ToolState.Paint.ArtObject ao:
                {
                    var id = NextId(Level.ArtObjects.Keys);
                    Level.ArtObjects[id] = new ArtObjectInstance
                    {
                        Name = ao.AssetName,
                        Position = position.ToRepacker(),
                        Rotation = RQuaternion.Identity,
                        Scale = RVector3.One
                    };

                    return new InstanceId.ArtObject(id);
                }

            case ToolState.Paint.BackgroundPlane bp:
                {
                    var id = NextId(Level.BackgroundPlanes.Keys);
                    Level.BackgroundPlanes[id] = new BackgroundPlane
                    {
                        TextureName = bp.AssetName,
                        Position = position.ToRepacker(),
                        Rotation = RQuaternion.Identity,
                        Scale = RVector3.One
                    };

                    return new InstanceId.BackgroundPlane(id);
                }

            case ToolState.Paint.NonPlayableCharacter npc:
                {
                    var id = NextId(Level.NonPlayerCharacters.Keys);
                    Level.NonPlayerCharacters[id] = new NpcInstance
                    {
                        Name = npc.AssetName,
                        Position = position.ToRepacker()
                    };

                    return new InstanceId.NonPlayableCharacter(id);
                }

            case ToolState.Paint.Volume:
                {
                    var id = NextId(Level.Volumes.Keys);
                    Level.Volumes[id] = new Volume
                    {
                        From = position.ToRepacker(),
                        To = (position + Vector3.One).ToRepacker(),
                        Orientations = [],
                        ActorSettings = new VolumeActorSettings()
                    };

                    return new InstanceId.Volume(id);
                }

            case ToolState.Paint.Path:
                {
                    var id = NextId(Level.Paths.Keys);
                    var center = hovered.Trile.Emplacement.AsVector().ToXna() + new Vector3(0.5f);
                    var hitPoint = center + (hovered.Face.AsVector() * 0.5f);
                    Level.Paths[id] = new MovementPath
                    {
                        Segments = new List<PathSegment>
                        {
                            new()
                            {
                                Destination = hitPoint.ToRepacker()
                            }
                        }
                    };

                    return new InstanceId.Path(id);
                }

            default:
                return null;
        }
    }

    private static int NextId(ICollection<int> instanceIds)
    {
        return instanceIds.Where(id => id != EddyEditor.InvalidId)
            .DefaultIfEmpty(-1)
            .Max() + 1;
    }
}