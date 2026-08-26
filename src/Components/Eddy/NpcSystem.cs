using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;

namespace FezEditor.Components.Eddy;

public class NpcSystem : EddySystem
{
    private IDisposable? _positionScope;

    private IDisposable? _destinationOffsetScope;

    public override void Initialize()
    {
        foreach (var id in Level.NonPlayerCharacters.Keys.Where(k => k != EddyEditor.InvalidId))
        {
            Visualize(new InstanceId.NonPlayableCharacter(id));
        }
    }

    public override void Visualize(InstanceId instanceId)
    {
        if (instanceId is not InstanceId.NonPlayableCharacter instance)
        {
            return;
        }

        if (!Level.NonPlayerCharacters.TryGetValue(instance.Id, out var npc))
        {
            Eddy.Registry.Destroy(instance);
            return;
        }

        var actor = Eddy.Registry.GetOrCreateActor(instance);
        actor.Name = $"{instance.Id}: {npc.Name}";
        actor.Transform.Position = npc.Position.ToXna();
        actor.Visible = Eddy.Visuals.HasFlag(EddyVisuals.NonPlayableCharacters);

        if (!actor.HasComponent<NpcMesh>())
        {
            var animations = Resources.LoadAnimations("Character Animations/" + npc.Name);
            var mesh = actor.AddComponent<NpcMesh>();
            mesh.Visualize(animations);
        }
    }

    public override void Inspect(params HashSet<InstanceId> instanceIds)
    {
        if (!instanceIds.TryGetSingle<InstanceId.NonPlayableCharacter>(out var instance))
        {
            return;
        }

        var npc = Level.NonPlayerCharacters[instance.Id];
        ImGui.Text($"NPC: {npc.Name} ID={instance.Id})");

        var position = npc.Position.ToXna();
        if (ImGuiX.InputFloat3("Position", ref position))
        {
            _positionScope ??= Eddy.History.BeginScope("Edit NPC Position");
            npc.Position = position.ToRepacker();
            Visualize(instance);
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _positionScope?.Dispose();
            _positionScope = null;
        }

        var destinationOffset = npc.DestinationOffset.ToXna();
        if (ImGuiX.DragFloat3("Destination Offset", ref destinationOffset))
        {
            _destinationOffsetScope ??= Eddy.History.BeginScope("Edit NPC Destination Offset");
            npc.DestinationOffset = destinationOffset.ToRepacker();
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _destinationOffsetScope?.Dispose();
            _destinationOffsetScope = null;
        }

        var walkSpeed = npc.WalkSpeed;
        if (ImGui.InputFloat("Walk Speed", ref walkSpeed))
        {
            using (Eddy.History.BeginScope("Edit NPC Walk Speed"))
            {
                npc.WalkSpeed = walkSpeed;
            }
        }

        var randomizeSpeech = npc.RandomizeSpeech;
        if (ImGui.Checkbox("Randomize Speech", ref randomizeSpeech))
        {
            using (Eddy.History.BeginScope("Edit NPC Randomize Speech"))
            {
                npc.RandomizeSpeech = randomizeSpeech;
            }
        }

        var sayFirstSpeechLineOnce = npc.SayFirstSpeechLineOnce;
        if (ImGui.Checkbox("Say First Speech Line Once", ref sayFirstSpeechLineOnce))
        {
            using (Eddy.History.BeginScope("Edit NPC Say First Speech Line Once"))
            {
                npc.SayFirstSpeechLineOnce = sayFirstSpeechLineOnce;
            }
        }

        var avoidsGomez = npc.AvoidsGomez;
        if (ImGui.Checkbox("Avoids Gomez", ref avoidsGomez))
        {
            using (Eddy.History.BeginScope("Edit NPC Avoids Gomez"))
            {
                npc.AvoidsGomez = avoidsGomez;
            }
        }

        var actorType = (int)npc.ActorType;
        var actors = Enum.GetNames<ActorType>();

        if (ImGui.Combo("Actor Type", ref actorType, actors, actors.Length))
        {
            using (Eddy.History.BeginScope("Edit NPC Actor Type"))
            {
                npc.ActorType = (ActorType)actorType;
            }
        }

        var speech = new Dirty<List<SpeechLine>>(npc.Speech);
        if (ImGuiX.EditableList("Speech", ref speech, RenderSpeechLine, () => new SpeechLine()))
        {
            using (Eddy.History.BeginScope("Edit NPC Speech"))
            {
                npc.Speech = speech.Value;
            }
        }

        // NpcAction is not IEquatable, so int key is being used
        var actions = new Dirty<Dictionary<int, NpcActionContent>>(
            npc.Actions.ToDictionary(kv => (int)kv.Key, kv => kv.Value));

        if (ImGuiX.EditableDict("Actions", ref actions, RenderNpcActionContent, RenderNewContent,
                () => new NpcActionContent()))
        {
            using (Eddy.History.BeginScope("Edit NPC Actions"))
            {
                npc.Actions = actions.Value.ToDictionary(kv => (NpcAction)kv.Key, kv => kv.Value);
            }
        }
    }

    private bool RenderSpeechLine(int index, ref SpeechLine item)
    {
        ImGui.TextDisabled(index + ":");
        var edited = false;

        {
            var text = item.Text.EmptyIfNull();
            if (ImGui.InputText("Text##sl" + index, ref text, 255))
            {
                item.Text = text.NullIfEmpty();
                edited = true;
            }
        }

        if (ImGuiX.NullableToggleButton("Override Content", item.OverrideContent))
        {
            var shouldAdd = item.OverrideContent == null;
            var actionName = shouldAdd ? "Add" : "Remove";
            using (Eddy.History.BeginScope($"{actionName} NPC Override Content"))
            {
                item.OverrideContent = shouldAdd ? new NpcActionContent() : null;
                edited = true;
            }
        }

        if (item.OverrideContent != null)
        {
            var animationName = item.OverrideContent.AnimationName.EmptyIfNull();
            if (ImGui.InputText("Animation Name##sl1" + index, ref animationName, 255))
            {
                item.OverrideContent.AnimationName = animationName.NullIfEmpty();
                edited = true;
            }

            var soundName = item.OverrideContent.SoundName.EmptyIfNull();
            if (ImGui.InputText("Sound Name##sl2" + index, ref soundName, 255))
            {
                item.OverrideContent.SoundName = soundName.NullIfEmpty();
                edited = true;
            }
        }

        return edited;
    }

    private static bool RenderNpcActionContent(int key, ref NpcActionContent value)
    {
        ImGui.TextDisabled((NpcAction)key + ":");
        var edited = false;

        var animationName = value.AnimationName.EmptyIfNull();
        if (ImGui.InputText("Animation Name##npc1" + key, ref animationName, 255))
        {
            value.AnimationName = animationName.NullIfEmpty();
            edited = true;
        }

        var soundName = value.SoundName.EmptyIfNull();
        if (ImGui.InputText("Sound Name##npc2" + key, ref soundName, 255))
        {
            value.SoundName = soundName.NullIfEmpty();
            edited = true;
        }

        return edited;
    }

    private static bool RenderNewContent(ref int key)
    {
        var actions = Enum.GetNames<NpcAction>();
        return ImGui.Combo("##npcAction", ref key, actions, actions.Length);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _positionScope?.Dispose();
        _destinationOffsetScope?.Dispose();
    }
}