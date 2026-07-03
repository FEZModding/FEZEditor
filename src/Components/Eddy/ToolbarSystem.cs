using FezEditor.Components.Eddy;
using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public class ToolbarSystem : EddySystem
{
    public override void Draw()
    {
        DrawToolButton(Lucide.MousePointer2, new ToolState.Select());

        ImGui.SameLine();
        DrawToolButton(Lucide.Move3D, new ToolState.Translate());

        ImGui.SameLine();
        DrawToolButton(Lucide.Rotate3D, new ToolState.Rotate());

        ImGui.SameLine();
        DrawToolButton(Lucide.Scale3D, new ToolState.Scale());

        ImGui.SameLine();
        ImGui.TextDisabled("|");

        ImGui.SameLine();
        DrawToolButton(Lucide.Pencil, Eddy.PickAndPaint(Eddy.SelectedEntry));

        ImGui.SameLine();
        DrawToolButton(Lucide.Pipette, new ToolState.Pick());

        ImGui.SameLine();
        {
            ImGui.BeginDisabled(Eddy.ShowAssetBrowser);
            if (ImGui.Button($"{Lucide.Sprout}"))
            {
                Eddy.ShowAssetBrowser = true;
            }

            ImGui.EndDisabled();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Asset Browser");
            }
        }

        ImGui.SameLine();
        {
            if (ImGui.Button($"{Lucide.SquareDashed}"))
            {
                Eddy.Tool = new ToolState.Paint.Volume();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Place Volume");
            }
        }


        ImGui.SameLine();
        {
            if (ImGui.Button($"{Lucide.Route}"))
            {
                Eddy.Tool = new ToolState.Paint.Path();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Start a new Path");
            }
        }

        ImGui.SameLine();
        ImGui.TextDisabled("|");

        ImGui.SameLine();
        {
            ImGui.BeginDisabled(Eddy.ShowInstanceBrowser);
            if (ImGui.Button($"{Lucide.Trees}"))
            {
                Eddy.ShowInstanceBrowser = true;
            }

            ImGui.EndDisabled();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Instance Browser");
            }
        }

        ImGui.SameLine();
        {
            ImGui.BeginDisabled(Eddy.ShowScriptBrowser);
            if (ImGui.Button($"{Lucide.CodeXml}"))
            {
                Eddy.ShowScriptBrowser = true;
            }

            ImGui.EndDisabled();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Script Browser");
            }
        }

        ImGui.SameLine();
        {
            ImGui.BeginDisabled(Eddy.ShowProperties);
            if (ImGui.Button($"{Lucide.List}"))
            {
                Eddy.ShowProperties = true;
            }

            ImGui.EndDisabled();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Show Properties Window");
            }
        }

        ImGui.SameLine();
        ImGui.TextDisabled("|");

        ImGui.SameLine();
        {
            var overlapIndex = Eddy.OverlapIndex;

            ImGui.Text("Overlap Layer:");
            if (ImGui.IsItemHovered() && overlapIndex == 0)
            {
                ImGui.SetTooltip("This is main layer");
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 4.7f);

            if (ImGui.InputInt("##Layer", ref overlapIndex))
            {
                Eddy.OverlapIndex = overlapIndex;
            }
        }

        ImGui.SameLine();
        ImGui.TextDisabled("|");

        ImGui.SameLine();
        {
            ImGui.BeginDisabled(Eddy.ShowProperties);
            if (ImGui.Button($"{Lucide.FileAxis3d}"))
            {
                var options = new FileDialog.Options
                {
                    Title = "Export level diorama",
                    DefaultLocation = Path.Combine(Resources.GetFullPath(""), $"{Level.Name}.glb"),
                    Filters = [new FileDialog.Filter("GLB file", "glb")]
                };

                FileDialog.Show(
                    FileDialog.Type.SaveFile,
                    files => Game.AddComponent(new PhilExporter(Game, Level, files[0])),
                    options
                );
            }

            ImGui.EndDisabled();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Export Level as Diorama");
            }
        }

        ImGui.SameLine();
        {
            ImGui.BeginDisabled(Eddy.ShowFarAwayPreviewer);
            if (ImGui.Button($"{Lucide.ScanEye}"))
            {
                Eddy.ShowFarAwayPreviewer = true;
            }

            ImGui.EndDisabled();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Generate level preview");
            }
        }

        var text = $"{Lucide.EllipsisVertical} {Eddy.CurrentView}";
        var viewButtonWidth = ImGui.CalcTextSize(text).X + (ImGui.GetStyle().FramePadding.X * 2);
        ImGui.SameLine(ImGui.GetContentRegionMax().X - viewButtonWidth);

        if (ImGui.Button(text))
        {
            ImGui.OpenPopup("##ViewOptions");
        }

        if (ImGui.BeginPopup("##ViewOptions"))
        {
            ImGui.SeparatorText($"{Lucide.Camera} Projections");
            {
                ImGui.BeginDisabled(Eddy.ShowFarAwayPreviewer);
                if (ImGui.Button("Perspective View"))
                {
                    Eddy.SwitchToPerspective();
                }
                ImGui.EndDisabled();

                if (ImGui.Button("Front View"))
                {
                    Eddy.SwitchToOrtho(ViewMode.Front, 0f);
                }

                if (ImGui.Button("Right View"))
                {
                    Eddy.SwitchToOrtho(ViewMode.Right, MathHelper.PiOver2);
                }

                if (ImGui.Button("Back View"))
                {
                    Eddy.SwitchToOrtho(ViewMode.Back, MathHelper.Pi);
                }

                if (ImGui.Button("Left View"))
                {
                    Eddy.SwitchToOrtho(ViewMode.Left, -MathHelper.PiOver2);
                }
            }

            ImGui.SeparatorText($"{Lucide.Pyramid} Visuals");
            {
                var visuals = (int)Eddy.Visuals;

                var edited = false;
                edited |= ImGui.CheckboxFlags("Triles", ref visuals, (int)EddyVisuals.Triles);
                edited |= ImGui.CheckboxFlags("Empty Triles", ref visuals, (int)EddyVisuals.EmptyTriles);
                edited |= ImGui.CheckboxFlags("Displaced Triles", ref visuals, (int)EddyVisuals.DisplacedTriles);
                edited |= ImGui.CheckboxFlags("Overlapped Triles", ref visuals, (int)EddyVisuals.OverlappedTriles);
                edited |= ImGui.CheckboxFlags("Art Objects", ref visuals, (int)EddyVisuals.ArtObjects);
                edited |= ImGui.CheckboxFlags("Background Planes", ref visuals, (int)EddyVisuals.BackgroundPlanes);
                edited |= ImGui.CheckboxFlags("Non-Playable Characters", ref visuals, (int)EddyVisuals.NonPlayableCharacters);
                edited |= ImGui.CheckboxFlags("Gomez", ref visuals, (int)EddyVisuals.Gomez);
                edited |= ImGui.CheckboxFlags("Liquid", ref visuals, (int)EddyVisuals.Liquid);
                edited |= ImGui.CheckboxFlags("Sky", ref visuals, (int)EddyVisuals.Sky);
                edited |= ImGui.CheckboxFlags("Rain", ref visuals, (int)EddyVisuals.Rain);

                ImGui.Separator();

                edited |= ImGui.CheckboxFlags("Volumes", ref visuals, (int)EddyVisuals.Volumes);
                edited |= ImGui.CheckboxFlags("Paths", ref visuals, (int)EddyVisuals.Paths);
                edited |= ImGui.CheckboxFlags("Level Bounds", ref visuals, (int)EddyVisuals.LevelBounds);
                edited |= ImGui.CheckboxFlags("Collision Map", ref visuals, (int)EddyVisuals.CollisionMap);
                edited |= ImGui.CheckboxFlags("Pickable Bounds", ref visuals, (int)EddyVisuals.PickableBounds);

                if (edited)
                {
                    Eddy.Visuals = (EddyVisuals)visuals;
                    Eddy.VisualizeAll();
                }
            }

            ImGui.Separator();

            var showRaycastDebug = Eddy.ShowRaycastDebug;
            ImGui.Checkbox("Raycast Debug", ref showRaycastDebug);
            Eddy.ShowRaycastDebug = showRaycastDebug;

            ImGui.EndPopup();
        }
    }

    private void DrawToolButton<T>(string icon, T tool) where T : ToolState
    {
        var active = Eddy.Tool is T;
        if (active)
        {
            unsafe
            {
                var color = *ImGui.GetStyleColorVec4(ImGuiCol.ButtonActive);
                ImGui.PushStyleColor(ImGuiCol.Button, color);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, color);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, color);
            }
        }

        ImGui.BeginDisabled(!(active || Eddy.IsToolEnabled(tool)) || Eddy.ShowFarAwayPreviewer);
        if (ImGui.Button($"{icon}##{typeof(T).Name}"))
        {
            Eddy.Tool = tool;
        }

        ImGui.EndDisabled();

        if (active)
        {
            ImGui.PopStyleColor(3);
        }

        if (ImGui.IsItemHovered())
        {
            var label = tool switch
            {
                ToolState.Select => "Select",
                ToolState.Translate => "Translate",
                ToolState.Rotate => "Rotate",
                ToolState.Scale => "Scale",
                ToolState.Paint.None => "Paint",
                ToolState.Paint.Trile t => $"Paint with \"{t.AssetName}\" trile",
                ToolState.Paint.ArtObject ao => $"Place a new \"{ao.AssetName}\" art object",
                ToolState.Paint.BackgroundPlane bp => $"Place a new \"{bp.AssetName}\" background plane",
                ToolState.Paint.NonPlayableCharacter npc => $"Place a new \"{npc.AssetName}\" npc",
                ToolState.Pick => "Pick",
                _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null)
            };

            ImGui.SetItemTooltip(label);
        }
    }
}