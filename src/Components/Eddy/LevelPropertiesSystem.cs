using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;

namespace FezEditor.Components.Eddy;

public class LevelPropertiesSystem : EddySystem
{
    public override void Inspect(params HashSet<InstanceId> instanceIds)
    {
        if (!instanceIds.TryGetSingle<InstanceId.Level>(out _))
        {
            return;
        }

        var size = Level.Size.ToXna();
        if (ImGuiX.InputFloat3("Size", ref size))
        {
            using (Eddy.History.BeginScope("Edit Level Size"))
            {
                Level.Size = size.ToRepacker();
            }
        }

        var sequenceSamplesPath = Level.SequenceSamplesPath.EmptyIfNull();
        if (ImGui.InputText("Sequence Samples Path", ref sequenceSamplesPath, 255))
        {
            using (Eddy.History.BeginScope("Edit Level Sequence Samples Path"))
            {
                Level.SequenceSamplesPath = sequenceSamplesPath.NullIfEmpty();
            }
        }

        var flat = Level.Flat;
        if (ImGui.Checkbox("Flat", ref flat))
        {
            using (Eddy.History.BeginScope("Edit Level Flat"))
            {
                Level.Flat = flat;
            }
        }

        var skipPostProcess = Level.SkipPostProcess;
        if (ImGui.Checkbox("Skip Post Process", ref skipPostProcess))
        {
            using (Eddy.History.BeginScope("Edit Level Skip Post Process"))
            {
                Level.SkipPostProcess = skipPostProcess;
            }
        }

        var baseDiffuse = Level.BaseDiffuse;
        if (ImGui.InputFloat("Base Diffuse", ref baseDiffuse))
        {
            using (Eddy.History.BeginScope("Edit Level Base Diffuse"))
            {
                Level.BaseDiffuse = baseDiffuse;
            }
        }

        var baseAmbient = Level.BaseAmbient;
        if (ImGui.InputFloat("Base Ambient", ref baseAmbient))
        {
            using (Eddy.History.BeginScope("Edit Level Base Ambient"))
            {
                Level.BaseAmbient = baseAmbient;
            }
        }

        var gomezHaloName = Level.GomezHaloName.EmptyIfNull();
        if (ImGui.InputText("Gomez Halo Name", ref gomezHaloName, 255))
        {
            using (Eddy.History.BeginScope("Edit Level Gomez Halo Name"))
            {
                Level.GomezHaloName = gomezHaloName.NullIfEmpty();
            }
        }

        var haloFiltering = Level.HaloFiltering;
        if (ImGui.Checkbox("Halo Filtering", ref haloFiltering))
        {
            using (Eddy.History.BeginScope("Edit Level Halo Filtering"))
            {
                Level.HaloFiltering = haloFiltering;
            }
        }

        var blinkingAlpha = Level.BlinkingAlpha;
        if (ImGui.Checkbox("Blinking Alpha", ref blinkingAlpha))
        {
            using (Eddy.History.BeginScope("Edit Level Blinking Alpha"))
            {
                Level.BlinkingAlpha = blinkingAlpha;
            }
        }

        var loops = Level.Loops;
        if (ImGui.Checkbox("Loops", ref loops))
        {
            using (Eddy.History.BeginScope("Edit Level Loops"))
            {
                Level.Loops = loops;
            }
        }

        var waterType = (int)Level.WaterType;
        var waterTypes = Enum.GetNames<LiquidType>();

        if (ImGui.Combo("Water Type", ref waterType, waterTypes, waterTypes.Length))
        {
            using (Eddy.History.BeginScope("Edit Level Water Type"))
            {
                Level.WaterType = (LiquidType)waterType;
            }
        }

        var waterHeight = Level.WaterHeight;
        if (ImGui.DragFloat("Water Height", ref waterHeight))
        {
            using (Eddy.History.BeginScope("Edit Level Water Height"))
            {
                Level.WaterHeight = Math.Max(0, waterHeight);
            }
        }

        ImGui.LabelText("Sky Name", Level.SkyName.EmptyIfNull());
        ImGui.SameLine();
        if (ImGui.Button("...##SkyPick"))
        {
            Resources.RequestAssetPathFromUser(
                "Select Level Sky", "Pick sky name to use by current level:",
                "Skies/", picked =>
                {
                    using (Eddy.History.BeginScope("Edit Level Sky Name"))
                    {
                        Level.SkyName = picked["Skies/".Length..];
                    }
                }
            );
        }

        var songName = Level.SongName.EmptyIfNull();
        if (ImGui.InputText("Song Name", ref songName, 255))
        {
            using (Eddy.History.BeginScope("Edit Level Song Name"))
            {
                Level.SongName = songName.NullIfEmpty();
            }
        }

        var fapFadeOutStart = Level.FAPFadeOutStart;
        if (ImGui.InputInt("FAP Fade Out Start", ref fapFadeOutStart))
        {
            using (Eddy.History.BeginScope("Edit Level FAP Fade Out Start"))
            {
                Level.FAPFadeOutStart = fapFadeOutStart;
            }
        }

        var fapFadeOutLength = Level.FAPFadeOutLength;
        if (ImGui.InputInt("FAP Fade Out Length", ref fapFadeOutLength))
        {
            using (Eddy.History.BeginScope("Edit Level FAP Fade Out Length"))
            {
                Level.FAPFadeOutLength = fapFadeOutLength;
            }
        }

        var descending = Level.Descending;
        if (ImGui.Checkbox("Descending", ref descending))
        {
            using (Eddy.History.BeginScope("Edit Level Descending"))
            {
                Level.Descending = descending;
            }
        }

        var rainy = Level.Rainy;
        if (ImGui.Checkbox("Rainy", ref rainy))
        {
            using (Eddy.History.BeginScope("Edit Level Rainy"))
            {
                Level.Rainy = rainy;
            }
        }

        var lowPass = Level.LowPass;
        if (ImGui.Checkbox("Low Pass", ref lowPass))
        {
            using (Eddy.History.BeginScope("Edit Level Low Pass"))
            {
                Level.LowPass = lowPass;
            }
        }

        var mutedLoops = new Dirty<List<string>>(Level.MutedLoops);
        if (ImGuiX.EditableList("Muted Loops", ref mutedLoops, RenderLoops, () => ""))
        {
            using (Eddy.History.BeginScope("Edit Level Muted Loops"))
            {
                Level.MutedLoops = mutedLoops;
            }
        }

        var ambienceTracks = new Dirty<List<AmbienceTrack>>(Level.AmbienceTracks);
        if (ImGuiX.EditableList("Ambience Tracks", ref ambienceTracks, RenderTracks, () => new AmbienceTrack()))
        {
            using (Eddy.History.BeginScope("Edit Level Ambience Tracks"))
            {
                Level.AmbienceTracks = ambienceTracks;
            }
        }

        var nodeType = (int)Level.NodeType;
        var nodeTypes = Enum.GetNames<LevelNodeType>();

        if (ImGui.Combo("Node Type", ref nodeType, nodeTypes, nodeTypes.Length))
        {
            using (Eddy.History.BeginScope("Edit Level Node Type"))
            {
                Level.NodeType = (LevelNodeType)nodeType;
            }
        }

        var quantum = Level.Quantum;
        if (ImGui.Checkbox("Quantum", ref quantum))
        {
            using (Eddy.History.BeginScope("Edit Level Quantum"))
            {
                Level.Quantum = quantum;
            }
        }
    }

    private static bool RenderLoops(int index, ref string item)
    {
        return ImGui.InputText("##loop" + index, ref item, 255);
    }

    private static bool RenderTracks(int index, ref AmbienceTrack item)
    {
        ImGui.TextDisabled(index + ":");

        var name = item.Name;
        if (ImGui.InputText("Name##name" + index, ref name, 255))
        {
            item.Name = name;
            return true;
        }

        var day = item.Day;
        if (ImGui.Checkbox("Day##day" + index, ref day))
        {
            item.Day = day;
            return true;
        }

        var dusk = item.Dusk;
        ImGui.SameLine();
        if (ImGui.Checkbox("Dusk##dusk" + index, ref dusk))
        {
            item.Dusk = dusk;
            return true;
        }

        var night = item.Night;
        ImGui.SameLine();
        if (ImGui.Checkbox("Night##night" + index, ref night))
        {
            item.Night = night;
            return true;
        }

        var dawn = item.Dawn;
        ImGui.SameLine();
        if (ImGui.Checkbox("Dawn##dawn" + index, ref dawn))
        {
            item.Dawn = dawn;
            return true;
        }

        return false;
    }
}