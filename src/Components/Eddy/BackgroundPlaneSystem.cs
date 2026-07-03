using FezEditor.Actors;
using FezEditor.Components.Eddy;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using ImGuiNET;

namespace FezEditor.Components.Eddy;

public class BackgroundPlaneSystem : EddySystem
{
    private readonly Camera _camera;

    private IDisposable? _positionScope;

    private IDisposable? _rotationScope;

    private IDisposable? _scaleScope;

    private IDisposable? _sizeScope;

    private IDisposable? _filterScope;

    public BackgroundPlaneSystem(Camera camera)
    {
        _camera = camera;
    }

    public override void Initialize()
    {
        foreach (var id in Level.BackgroundPlanes.Keys.Where(k => k != EddyEditor.InvalidId))
        {
            Visualize(new InstanceId.BackgroundPlane(id));
        }
    }

    public override void Visualize(InstanceId instanceId)
    {
        if (instanceId is not InstanceId.BackgroundPlane instance)
        {
            return;
        }

        if (!Level.BackgroundPlanes.TryGetValue(instance.Id, out var bgPlane))
        {
            Eddy.Registry.Destroy(instance);
            return;
        }

        var actor = Eddy.Registry.GetOrCreateActor(instance);
        actor.Name = $"{instance.Id}: {bgPlane.TextureName}";
        actor.Transform.Position = bgPlane.Position.ToXna();
        actor.Transform.Rotation = bgPlane.Rotation.ToXna();
        actor.Transform.Scale = bgPlane.Scale.ToXna();
        actor.Visible = Eddy.Visuals.HasFlag(EddyVisuals.BackgroundPlanes);

        if (!actor.TryGetComponent<BackgroundPlaneMesh>(out var mesh))
        {
            var asset = Resources.Load<object>("Background Planes/" + bgPlane.TextureName);
            mesh = actor.AddComponent<BackgroundPlaneMesh>();
            mesh.Visualize(asset);
        }

        mesh!.Camera = _camera;
        mesh.Billboard = bgPlane.Billboard;
        mesh.DoubleSided = bgPlane.Doublesided;
        mesh.Color = bgPlane.Filter.ToXna();
        mesh.Opacity = bgPlane.Opacity;
        mesh.LightMap = bgPlane.LightMap;
        mesh.AllowOverbrightness = bgPlane.AllowOverbrightness;
        mesh.Fullbright = bgPlane.Fullbright;
        mesh.PixelatedLightmap = bgPlane.PixelatedLightmap;
        mesh.ClampTexture = bgPlane.ClampTexture;
        mesh.XTextureRepeat = bgPlane.XTextureRepeat;
        mesh.YTextureRepeat = bgPlane.YTextureRepeat;
    }

    public override void Inspect(params HashSet<InstanceId> instanceIds)
    {
        if (!instanceIds.TryGetSingle<InstanceId.BackgroundPlane>(out var instance))
        {
            return;
        }

        var bgPlane = Level.BackgroundPlanes[instance.Id];
        var actor = Eddy.Registry.GetActor(instance);
        var backgroundPlaneMesh = actor.GetComponent<BackgroundPlaneMesh>();

        ImGui.Text($"Background Plane: {bgPlane.TextureName} (ID={instance.Id})");

        var position = bgPlane.Position.ToXna();
        if (ImGuiX.DragFloat3("Position", ref position))
        {
            _positionScope ??= Eddy.History.BeginScope("Edit BG Position");
            bgPlane.Position = position.ToRepacker();
            Visualize(instance);
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _positionScope?.Dispose();
            _positionScope = null;
        }

        var angles = bgPlane.Rotation.ToXna().ToYawPitchRollDegrees();
        if (ImGuiX.DragFloat3("Rotation (Yaw, Pitch, Roll)", ref angles, 1f, -180f, 180f, "%.1f"))
        {
            _rotationScope ??= Eddy.History.BeginScope("Edit BG Rotation");
            bgPlane.Rotation = Mathz.FromYawPitchRollDegrees(angles).ToRepacker();
            Visualize(instance);
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _rotationScope?.Dispose();
            _rotationScope = null;
        }

        var scale = bgPlane.Scale.ToXna();
        if (ImGuiX.DragFloat3("Scale", ref scale, 0.01f))
        {
            _scaleScope ??= Eddy.History.BeginScope("Edit BG Scale");
            bgPlane.Scale = scale.ToRepacker();
            Visualize(instance);
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _scaleScope?.Dispose();
            _scaleScope = null;
        }

        var size = bgPlane.Size.ToXna();
        if (ImGuiX.DragFloat3("Size", ref size, 0.01f))
        {
            _sizeScope ??= Eddy.History.BeginScope("Edit BG Size");
            bgPlane.Size = size.ToRepacker();
            Visualize(instance);
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _sizeScope?.Dispose();
            _sizeScope = null;
        }

        var lightMap = bgPlane.LightMap;
        if (ImGui.Checkbox("Light Map", ref lightMap))
        {
            using (Eddy.History.BeginScope("Edit BG Light Map"))
            {
                bgPlane.LightMap = lightMap;
            }
        }

        var allowOverbrightness = bgPlane.AllowOverbrightness;
        if (ImGui.Checkbox("Allow Overbrightness", ref allowOverbrightness))
        {
            using (Eddy.History.BeginScope("Edit BG Allow Overbrightness"))
            {
                bgPlane.AllowOverbrightness = allowOverbrightness;
            }
        }

        var filter = bgPlane.Filter.ToXna();
        if (ImGuiX.ColorEdit4("Filter", ref filter))
        {
            _filterScope ??= Eddy.History.BeginScope("Edit BG Filter");
            bgPlane.Filter = filter.ToRepacker();
            Visualize(instance);
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _filterScope?.Dispose();
            _filterScope = null;
        }

        var animated = backgroundPlaneMesh.Animated;
        ImGui.BeginDisabled();
        ImGui.Checkbox("Animated", ref animated);
        ImGui.EndDisabled();

        var doublesided = bgPlane.Doublesided;
        if (ImGui.Checkbox("Double Sided", ref doublesided))
        {
            using (Eddy.History.BeginScope("Edit BG Double Sided"))
            {
                bgPlane.Doublesided = doublesided;
            }
        }

        var opacity = bgPlane.Opacity;
        if (ImGui.InputFloat("Opacity", ref opacity))
        {
            using (Eddy.History.BeginScope("Edit BG Opacity"))
            {
                bgPlane.Opacity = opacity;
            }
        }

        var attachedGroup = bgPlane.AttachedGroup ?? EddyEditor.InvalidId;
        if (ImGui.InputInt("Attached Group", ref attachedGroup))
        {
            using (Eddy.History.BeginScope("Edit BG Attached Group"))
            {
                bgPlane.AttachedGroup = attachedGroup == EddyEditor.InvalidId ? null : attachedGroup;
            }
        }

        var billboard = bgPlane.Billboard;
        if (ImGui.Checkbox("Billboard", ref billboard))
        {
            using (Eddy.History.BeginScope("Edit BG Billboard"))
            {
                bgPlane.Billboard = billboard;
            }
        }

        var syncWithSamples = bgPlane.SyncWithSamples;
        if (ImGui.Checkbox("Sync With Samples", ref syncWithSamples))
        {
            using (Eddy.History.BeginScope("Edit BG Sync With Samples"))
            {
                bgPlane.SyncWithSamples = syncWithSamples;
            }
        }

        var crosshatch = bgPlane.Crosshatch;
        if (ImGui.Checkbox("Crosshatch", ref crosshatch))
        {
            using (Eddy.History.BeginScope("Edit BG Crosshatch"))
            {
                bgPlane.Crosshatch = crosshatch;
            }
        }

        var alwaysOnTop = bgPlane.AlwaysOnTop;
        if (ImGui.Checkbox("Always On Top", ref alwaysOnTop))
        {
            using (Eddy.History.BeginScope("Edit BG Always On Top"))
            {
                bgPlane.AlwaysOnTop = alwaysOnTop;
            }
        }

        var fullbright = bgPlane.Fullbright;
        if (ImGui.Checkbox("Fullbright", ref fullbright))
        {
            using (Eddy.History.BeginScope("Edit BG Fullbright"))
            {
                bgPlane.Fullbright = fullbright;
            }
        }

        var pixelatedLightmap = bgPlane.PixelatedLightmap;
        if (ImGui.Checkbox("Pixelated Lightmap", ref pixelatedLightmap))
        {
            using (Eddy.History.BeginScope("Edit BG Pixelated Lightmap"))
            {
                bgPlane.PixelatedLightmap = pixelatedLightmap;
            }
        }

        var xTextureRepeat = bgPlane.XTextureRepeat;
        if (ImGui.Checkbox("Xtexture Repeat", ref xTextureRepeat))
        {
            using (Eddy.History.BeginScope("Edit BG Xtexture Repeat"))
            {
                bgPlane.XTextureRepeat = xTextureRepeat;
            }
        }

        var yTextureRepeat = bgPlane.YTextureRepeat;
        if (ImGui.Checkbox("Ytexture Repeat", ref yTextureRepeat))
        {
            using (Eddy.History.BeginScope("Edit BG Ytexture Repeat"))
            {
                bgPlane.YTextureRepeat = yTextureRepeat;
            }
        }

        var clampTexture = bgPlane.ClampTexture;
        if (ImGui.Checkbox("Clamp Texture", ref clampTexture))
        {
            using (Eddy.History.BeginScope("Edit BG Clamp Texture"))
            {
                bgPlane.ClampTexture = clampTexture;
            }
        }

        var actorType = (int)bgPlane.ActorType;
        var actors = Enum.GetNames<ActorType>();
        if (ImGui.Combo("Actor Type", ref actorType, actors, actors.Length))
        {
            using (Eddy.History.BeginScope("Edit BG Actor Type"))
            {
                bgPlane.ActorType = (ActorType)actorType;
            }
        }

        var attachedPlane = bgPlane.AttachedPlane ?? EddyEditor.InvalidId;
        if (ImGui.InputInt("Attached Plane", ref attachedPlane))
        {
            using (Eddy.History.BeginScope("Edit BG Attached Plane"))
            {
                bgPlane.AttachedPlane = attachedPlane == EddyEditor.InvalidId ? null : attachedPlane;
            }
        }

        var parallaxFactor = bgPlane.ParallaxFactor;
        if (ImGui.InputFloat("Parallax Factor", ref parallaxFactor))
        {
            using (Eddy.History.BeginScope("Edit BG Parallax Factor"))
            {
                bgPlane.ParallaxFactor = parallaxFactor;
            }
        }
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _positionScope?.Dispose();
        _rotationScope?.Dispose();
        _scaleScope?.Dispose();
        _sizeScope?.Dispose();
        _filterScope?.Dispose();
    }
}