using FezEditor.Actors;
using FezEditor.Components.Eddy;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;

namespace FezEditor.Components.Eddy;

public class ArtObjectSystem : EddySystem
{
    private IDisposable? _positionScope;

    private IDisposable? _rotationScope;

    private IDisposable? _scaleScope;

    public override void Initialize()
    {
        foreach (var id in Level.ArtObjects.Keys.Where(k => k != EddyEditor.InvalidId))
        {
            Visualize(new InstanceId.ArtObject(id));
        }
    }

    public override void Visualize(InstanceId instanceId)
    {
        if (instanceId is not InstanceId.ArtObject instance)
        {
            return;
        }

        if (!Level.ArtObjects.TryGetValue(instance.Id, out var artObject))
        {
            Eddy.Registry.Destroy(instance);
            return;
        }

        var actor = Eddy.Registry.GetOrCreateActor(instance);
        actor.Name = $"{instance.Id}: {artObject.Name}";
        actor.Transform.Position = artObject.Position.ToXna();
        actor.Transform.Rotation = artObject.Rotation.ToXna();
        actor.Transform.Scale = artObject.Scale.ToXna();
        actor.Visible = Eddy.Visuals.HasFlag(EddyVisuals.ArtObjects);

        if (!actor.HasComponent<ArtObjectMesh>())
        {
            var ao = Resources.Load<ArtObject>("Art Objects/" + artObject.Name);
            var mesh = actor.AddComponent<ArtObjectMesh>();
            mesh.Visualize(ao);
        }
    }

    public override void Inspect(params HashSet<InstanceId> instanceIds)
    {
        if (!instanceIds.TryGetSingle<InstanceId.ArtObject>(out var instance))
        {
            return;
        }

        var artObject = Level.ArtObjects[instance.Id];
        ImGui.Text($"Art Object: {artObject.Name} (ID={instance.Id})");

        var position = artObject.Position.ToXna();
        if (ImGuiX.DragFloat3("Position", ref position))
        {
            _positionScope ??= Eddy.History.BeginScope("Edit AO Position");
            artObject.Position = position.ToRepacker();
            Visualize(instance);
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _positionScope?.Dispose();
            _positionScope = null;
        }

        var angles = artObject.Rotation.ToXna().ToYawPitchRollDegrees();
        if (ImGuiX.DragFloat3("Rotation (Yaw, Pitch, Roll)", ref angles, 1f, -180f, 180f, "%.1f"))
        {
            _rotationScope ??= Eddy.History.BeginScope("Edit AO Rotation");
            artObject.Rotation = Mathz.FromYawPitchRollDegrees(angles).ToRepacker();
            Visualize(instance);
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _rotationScope?.Dispose();
            _rotationScope = null;
        }

        var scale = artObject.Scale.ToXna();
        if (ImGuiX.DragFloat3("Scale", ref scale, 0.01f))
        {
            _scaleScope ??= Eddy.History.BeginScope("Edit AO Scale");
            artObject.Scale = scale.ToRepacker();
            Visualize(instance);
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _scaleScope?.Dispose();
            _scaleScope = null;
        }

        ImGui.SeparatorText("Actor Settings");

        var settings = artObject.ActorSettings;

        /* Actor settings stuff*/
        {
            var inactive = settings.Inactive;
            if (ImGui.Checkbox("Inactive", ref inactive))
            {
                using (Eddy.History.BeginScope("Edit AO Inactive"))
                {
                    settings.Inactive = inactive;
                }
            }

            var containedTrile = (int)settings.ContainedTrile;
            var actorNames = Enum.GetNames<ActorType>();
            if (ImGui.Combo("Contained Trile", ref containedTrile, actorNames, actorNames.Length))
            {
                using (Eddy.History.BeginScope("Edit AO Contained Trile"))
                {
                    settings.ContainedTrile = (ActorType)containedTrile;
                }
            }

            var attachedGroup = settings.AttachedGroup ?? EddyEditor.InvalidId;
            if (ImGui.InputInt("Attached Group", ref attachedGroup))
            {
                using (Eddy.History.BeginScope("Edit AO Attached Group"))
                {
                    settings.AttachedGroup = attachedGroup == EddyEditor.InvalidId ? null : attachedGroup;
                }
            }

            var spinEvery = settings.SpinEvery;
            if (ImGui.DragFloat("Spin Every", ref spinEvery, 0.1f))
            {
                using (Eddy.History.BeginScope("Edit AO Spin Every"))
                {
                    settings.SpinEvery = spinEvery;
                }
            }

            var spinOffset = settings.SpinOffset;
            if (ImGui.DragFloat("Spin Offset", ref spinOffset, 0.1f))
            {
                using (Eddy.History.BeginScope("Edit AO Spin Offset"))
                {
                    settings.SpinOffset = spinOffset;
                }
            }

            var offCenter = settings.OffCenter;
            if (ImGui.Checkbox("Off Center", ref offCenter))
            {
                using (Eddy.History.BeginScope("Edit AO Off Center"))
                {
                    settings.OffCenter = offCenter;
                }
            }

            var spinView = (int)settings.SpinView;
            var viewpoints = Enum.GetNames<Viewpoint>();
            if (ImGui.Combo("Spin View", ref spinView, viewpoints, viewpoints.Length))
            {
                using (Eddy.History.BeginScope("Edit AO Spin View"))
                {
                    settings.SpinView = (Viewpoint)spinView;
                }
            }

            var rotationCenter = settings.RotationCenter.ToXna();
            if (ImGuiX.DragFloat3("Rotation Center", ref rotationCenter, 0.01f))
            {
                using (Eddy.History.BeginScope("Edit AO Rotation Center"))
                {
                    settings.RotationCenter = rotationCenter.ToRepacker();
                }
            }

            var nextNode = settings.NextNode ?? EddyEditor.InvalidId;
            if (ImGui.InputInt("Next Node", ref nextNode))
            {
                using (Eddy.History.BeginScope("Edit AO Next Node"))
                {
                    settings.NextNode = nextNode == EddyEditor.InvalidId ? null : nextNode;
                }
            }

            var destinationLevel = settings.DestinationLevel.EmptyIfNull();
            if (ImGui.InputText("Destination Level", ref destinationLevel, 255))
            {
                using (Eddy.History.BeginScope("Edit AO Destination Level"))
                {
                    settings.DestinationLevel = destinationLevel.NullIfEmpty();
                }
            }

            var treasureMapName = settings.TreasureMapName.EmptyIfNull();
            if (ImGui.InputText("Treasure Map Name", ref treasureMapName, 255))
            {
                using (Eddy.History.BeginScope("Edit AO Treasure Map Name"))
                {
                    settings.TreasureMapName = treasureMapName.NullIfEmpty();
                }
            }

            var timeswitchWindBackSpeed = settings.TimeswitchWindBackSpeed;
            if (ImGui.DragFloat("Timeswitch Wind Back Speed", ref timeswitchWindBackSpeed, 0.01f))
            {
                using (Eddy.History.BeginScope("Edit AO Timeswitch Wind Back Speed"))
                {
                    settings.TimeswitchWindBackSpeed = timeswitchWindBackSpeed;
                }
            }

            var vibrationPattern = new Dirty<VibrationMotor[]>(settings.VibrationPattern.EmptyIfNull());
            if (ImGuiX.EditableArray("Vibration Pattern", ref vibrationPattern, RenderVibrationMotorItem,
                    () => default))
            {
                using (Eddy.History.BeginScope("Edit AO Vibration Pattern"))
                {
                    settings.VibrationPattern = vibrationPattern.Value.NullIfEmpty();
                }
            }

            var codePattern = new Dirty<CodeInput[]>(settings.CodePattern.EmptyIfNull());
            if (ImGuiX.EditableArray("Code Pattern", ref codePattern, RenderCodeInputItem, () => default))
            {
                using (Eddy.History.BeginScope("Edit AO Code Pattern"))
                {
                    settings.CodePattern = codePattern.Value.NullIfEmpty();
                }
            }

            var invisibleSides = new Dirty<FaceOrientation[]>(settings.InvisibleSides);
            if (ImGuiX.EditableArray("Invisible Sides", ref invisibleSides, RenderFaceOrientationItem, () => default))
            {
                using (Eddy.History.BeginScope("Edit AO Invisible Sides"))
                {
                    settings.InvisibleSides = invisibleSides.Value.Distinct().ToArray();
                }
            }

            ImGui.SeparatorText("Segment");

            if (ImGuiX.NullableToggleButton("Segment", settings.Segment))
            {
                var shouldAdd = settings.Segment == null;
                var actionName = shouldAdd ? "Add" : "Remove";
                using (Eddy.History.BeginScope($"{actionName} AO Segment"))
                {
                    settings.Segment = shouldAdd ? new PathSegment() : null;
                }
            }

            if (settings.Segment != null)
            {
                // Destination is recalculated at runtime by MovingGroupsHost from world-space AO positions
                // Editing it here has no effect.
                //
                // var destination = segment.Destination.ToXna();
                // if (ImGuiX.DragFloat3("Destination", ref destination, 0.01f))
                // {
                //     using (eddy.History.BeginScope("Edit AO Segment Destination"))
                //     {
                //         segment.Destination = destination.ToRepacker();
                //     }
                // }

                var duration = settings.Segment.Duration;
                if (ImGuiX.TimeSpanInput("Duration", ref duration))
                {
                    using (Eddy.History.BeginScope("Edit AO Segment Duration"))
                    {
                        settings.Segment.Duration = duration;
                    }
                }

                var waitTimeOnStart = settings.Segment.WaitTimeOnStart;
                if (ImGuiX.TimeSpanInput("Wait Time On Start", ref waitTimeOnStart))
                {
                    using (Eddy.History.BeginScope("Edit AO Segment Wait Time On Start"))
                    {
                        settings.Segment.WaitTimeOnStart = waitTimeOnStart;
                    }
                }

                var waitTimeOnFinish = settings.Segment.WaitTimeOnFinish;
                if (ImGuiX.TimeSpanInput("Wait Time On Finish", ref waitTimeOnFinish))
                {
                    using (Eddy.History.BeginScope("Edit AO Segment Wait Time On Finish"))
                    {
                        settings.Segment.WaitTimeOnFinish = waitTimeOnFinish;
                    }
                }

                var acceleration = settings.Segment.Acceleration;
                if (ImGui.DragFloat("Acceleration", ref acceleration, 0.01f))
                {
                    using (Eddy.History.BeginScope("Edit AO Segment Acceleration"))
                    {
                        settings.Segment.Acceleration = acceleration;
                    }
                }

                var deceleration = settings.Segment.Deceleration;
                if (ImGui.DragFloat("Deceleration", ref deceleration, 0.01f))
                {
                    using (Eddy.History.BeginScope("Edit AO Segment Deceleration"))
                    {
                        settings.Segment.Deceleration = deceleration;
                    }
                }

                var jitterFactor = settings.Segment.JitterFactor;
                if (ImGui.DragFloat("Jitter Factor", ref jitterFactor, 0.01f))
                {
                    using (Eddy.History.BeginScope("Edit AO Segment Jitter Factor"))
                    {
                        settings.Segment.JitterFactor = jitterFactor;
                    }
                }

                // Orientation is not used by MovingGroupsHost for connective rail segments.
                //
                // var orientation = segment.Orientation.ToXna();
                // var orientationEuler = orientation.ToEuler();
                // if (ImGuiX.DragFloat3("Orientation (Euler)", ref orientationEuler, 1f))
                // {
                //     using (eddy.History.BeginScope("Edit AO Segment Orientation"))
                //     {
                //         segment.Orientation = orientationEuler.FromEuler().ToRepacker();
                //     }
                // }

                var hasCustomData = settings.Segment.CustomData != null;
                if (ImGui.Checkbox("Custom Camera Data", ref hasCustomData))
                {
                    using (Eddy.History.BeginScope("Edit AO Segment Custom Data"))
                    {
                        settings.Segment.CustomData = hasCustomData ? new CameraNodeData() : null;
                    }
                }

                if (settings.Segment.CustomData is { } customData)
                {
                    var perspective = customData.Perspective;
                    if (ImGui.Checkbox("Perspective##cd", ref perspective))
                    {
                        using (Eddy.History.BeginScope("Edit AO Segment Custom Data Perspective"))
                        {
                            customData.Perspective = perspective;
                        }
                    }

                    var pixelsPerTrixel = customData.PixelsPerTrixel;
                    if (ImGui.InputInt("Pixels Per Trixel##cd", ref pixelsPerTrixel))
                    {
                        using (Eddy.History.BeginScope("Edit AO Segment Custom Data Pixels Per Trixel"))
                        {
                            customData.PixelsPerTrixel = pixelsPerTrixel;
                        }
                    }

                    var soundName = customData.SoundName.EmptyIfNull();
                    if (ImGui.InputText("Sound Name##cd", ref soundName, 255))
                    {
                        using (Eddy.History.BeginScope("Edit AO Segment Custom Data Sound Name"))
                        {
                            customData.SoundName = soundName.NullIfEmpty();
                        }
                    }
                }
            }
        }
    }

    private static bool RenderVibrationMotorItem(int index, ref VibrationMotor item)
    {
        ImGui.TextDisabled(index + ":");
        ImGui.SameLine();
        var motor = (int)item;
        var motors = Enum.GetNames<VibrationMotor>();
        var edited = ImGui.Combo("##vm" + index, ref motor, motors, motors.Length);
        item = (VibrationMotor)motor;
        return edited;
    }

    private static bool RenderCodeInputItem(int index, ref CodeInput item)
    {
        ImGui.TextDisabled(index + ":");
        ImGui.SameLine();
        var input = (int)item;
        var inputs = Enum.GetNames<CodeInput>();
        var edited = ImGui.Combo("##ci" + index, ref input, inputs, inputs.Length);
        item = (CodeInput)input;
        return edited;
    }

    private static bool RenderFaceOrientationItem(int index, ref FaceOrientation item)
    {
        ImGui.TextDisabled(index + ":");
        ImGui.SameLine();
        var face = (int)item;
        var faces = Enum.GetNames<FaceOrientation>();
        var edited = ImGui.Combo("##fo" + index, ref face, faces, faces.Length);
        item = (FaceOrientation)face;
        return edited;
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _positionScope?.Dispose();
        _rotationScope?.Dispose();
        _scaleScope?.Dispose();
    }
}