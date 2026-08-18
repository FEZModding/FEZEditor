using FezEditor.Actors;
using FezEditor.Components.Eddy;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public class VolumeSystem : EddySystem
{
    public override void Initialize()
    {
        foreach (var id in Level.Volumes.Keys.Where(k => k != EddyEditor.InvalidId))
        {
            Visualize(new InstanceId.Volume(id));
        }
    }

    public override void Visualize(InstanceId instanceId)
    {
        if (instanceId is not InstanceId.Volume instance)
        {
            return;
        }

        if (!Level.Volumes.TryGetValue(instance.Id, out var volume))
        {
            Eddy.Registry.Destroy(instance);
            return;
        }

        var actor = Eddy.Registry.GetOrCreateActor(instance);
        actor.Name = $"{instance.Id}: Volume";
        actor.Transform.Position = (volume.From.ToXna() + volume.To.ToXna()) / 2f;
        actor.Visible = Eddy.Visuals.HasFlag(EddyVisuals.Volumes);

        if (!actor.TryGetComponent<VolumeMesh>(out var mesh))
        {
            mesh = actor.AddComponent<VolumeMesh>();
        }

        mesh!.Size = (volume.To - volume.From).ToXna();
        mesh.Color = Color.LimeGreen;
        mesh.IsBlackHole = volume.ActorSettings is { IsBlackHole: true };
    }

    public override void Inspect(params HashSet<InstanceId> instanceIds)
    {
        if (!instanceIds.TryGetSingle<InstanceId.Volume>(out var instance))
        {
            return;
        }

        var volume = Level.Volumes[instance.Id];
        ImGui.Text($"Volume: ID={instance.Id}");

        var from = volume.From.ToXna();
        if (ImGuiX.InputFloat3("From", ref from))
        {
            using (Eddy.History.BeginScope("Edit From"))
            {
                volume.From = from.ToRepacker();
            }
        }

        var to = volume.To.ToXna();
        if (ImGuiX.InputFloat3("To", ref to))
        {
            using (Eddy.History.BeginScope("Edit To"))
            {
                volume.To = to.ToRepacker();
            }
        }

        var orientations = new Dirty<FaceOrientation[]>(volume.Orientations);
        if (ImGuiX.EditableArray("Orientations", ref orientations, RenderFace, () => default))
        {
            using (Eddy.History.BeginScope("Edit Orientations"))
            {
                volume.Orientations = orientations.Value;
            }
        }

        ImGui.SeparatorText("Actor Settings");

        if (ImGuiX.NullableToggleButton("ActorSettings", volume.ActorSettings))
        {
            var shouldAdd = volume.ActorSettings == null;
            var actionName = shouldAdd ? "Add" : "Remove";
            using (Eddy.History.BeginScope($"{actionName} ActorSettings"))
            {
                volume.ActorSettings = shouldAdd ? new VolumeActorSettings() : null;
            }
        }

        var settings = volume.ActorSettings;
        if (settings != null)
        {
            var farawayPlaneOffset = settings.FarawayPlaneOffset.ToXna();
            if (ImGuiX.InputFloat2("Faraway Plane Offset", ref farawayPlaneOffset))
            {
                using (Eddy.History.BeginScope("Edit Faraway Plane Offset"))
                {
                    settings.FarawayPlaneOffset = farawayPlaneOffset.ToRepacker();
                }
            }

            var isPointOfInterest = settings.IsPointOfInterest;
            if (ImGui.Checkbox("Is Point Of Interest", ref isPointOfInterest))
            {
                using (Eddy.History.BeginScope("Edit Is Point Of Interest"))
                {
                    settings.IsPointOfInterest = isPointOfInterest;
                }
            }

            var dotDialogue = new Dirty<List<DotDialogueLine>>(settings.DotDialogue);
            if (ImGuiX.EditableList("Dot Dialogue", ref dotDialogue, RenderDotDialog, () => new DotDialogueLine()))
            {
                using (Eddy.History.BeginScope("Edit Dot Dialogue"))
                {
                    settings.DotDialogue = dotDialogue;
                }
            }

            var waterLocked = settings.WaterLocked;
            if (ImGui.Checkbox("Water Locked", ref waterLocked))
            {
                using (Eddy.History.BeginScope("Edit Water Locked"))
                {
                    settings.WaterLocked = waterLocked;
                }
            }

            var codePattern = new Dirty<CodeInput[]>(settings.CodePattern.EmptyIfNull());
            if (ImGuiX.EditableArray("Code Pattern", ref codePattern, RenderCodePattern, () => default))
            {
                using (Eddy.History.BeginScope("Edit Code Pattern"))
                {
                    settings.CodePattern = codePattern.Value.NullIfEmpty();
                }
            }

            var isBlackHole = settings.IsBlackHole;
            if (ImGui.Checkbox("Is Black Hole", ref isBlackHole))
            {
                using (Eddy.History.BeginScope("Edit Is Black Hole"))
                {
                    settings.IsBlackHole = isBlackHole;
                }
            }

            var needsTrigger = settings.NeedsTrigger;
            if (ImGui.Checkbox("Needs Trigger", ref needsTrigger))
            {
                using (Eddy.History.BeginScope("Edit Needs Trigger"))
                {
                    settings.NeedsTrigger = needsTrigger;
                }
            }

            var isSecretPassage = settings.IsSecretPassage;
            if (ImGui.Checkbox("Is Secret Passage", ref isSecretPassage))
            {
                using (Eddy.History.BeginScope("Edit Is Secret Passage"))
                {
                    settings.IsSecretPassage = isSecretPassage;
                }
            }
        }
    }

    private static bool RenderFace(int index, ref FaceOrientation item)
    {
        ImGui.TextDisabled(index + ":");
        ImGui.SameLine();

        var face = (int)item;
        var faces = Enum.GetNames<FaceOrientation>();

        var edited = ImGui.Combo("##fo" + index, ref face, faces, faces.Length);
        item = (FaceOrientation)face;
        return edited;
    }

    private static bool RenderDotDialog(int index, ref DotDialogueLine item)
    {
        ImGui.TextDisabled(index + ":");

        var resourceText = item.ResourceText.EmptyIfNull();
        if (ImGui.InputText("Resource Text##dd1" + index, ref resourceText, 255))
        {
            item.ResourceText = resourceText.NullIfEmpty();
            return true;
        }

        var grouped = item.Grouped;
        if (ImGui.Checkbox("Grouped##dd2" + index, ref grouped))
        {
            item.Grouped = grouped;
            return true;
        }

        return false;
    }

    private static bool RenderCodePattern(int index, ref CodeInput item)
    {
        ImGui.TextDisabled(index + ":");
        ImGui.SameLine();

        var inputValues = Enum.GetValues<CodeInput>();
        var inputNames = Enum.GetNames<CodeInput>();

        var input = Array.IndexOf(inputValues, item);

        var edited = ImGui.Combo("##cp" + index, ref input, inputNames, inputNames.Length);
        if (input >= 0 && input < inputNames.Length)
        {
            item = inputValues[input];
        }

        return edited;
    }
}