using FezEditor.Actors;
using FezEditor.Components.Eddy;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public class GomezSystem : EddySystem
{
    public override void Initialize()
    {
        Visualize(new InstanceId.Gomez());
    }

    public override void Visualize(InstanceId instanceId)
    {
        if (instanceId is not InstanceId.Gomez gomez)
        {
            return;
        }

        var actor = Eddy.Registry.GetOrCreateActor(gomez);
        actor.Name = "Gomez";
        actor.Visible = Level.StartingFace != null && Eddy.Visuals.HasFlag(EddyVisuals.Gomez);

        if (Level.StartingFace == null)
        {
            actor.RemoveComponent<GomezMesh>();
        }
        else
        {
            actor.Transform.Position = Level.StartingFace.Id.ToXna().ToVector3() + Vector3.Up;
            actor.Transform.Rotation = Level.StartingFace.Face.AsQuaternion();

            if (!actor.HasComponent<GomezMesh>())
            {
                actor.AddComponent<GomezMesh>();
            }
        }
    }

    public override void Inspect(params HashSet<InstanceId> instanceIds)
    {
        if (!instanceIds.TryGetSingle<InstanceId.Gomez>(out _))
        {
            return;
        }

        ImGui.Text("Gomez (Starting Position)");
        ImGui.SetNextItemWidth(-1);
        if (ImGuiX.NullableToggleButton("Starting Face", Level.StartingFace))
        {
            var shouldAdd = Level.StartingFace == null;
            var actionName = shouldAdd ? "Add " : "Remove";
            using (Eddy.History.BeginScope($"{actionName} Gomez Starting Face"))
            {
                Level.StartingFace = shouldAdd
                    ? new TrileFace { Face = FaceOrientation.Front, Id = new TrileEmplacement() }
                    : null;
            }
        }

        if (Level.StartingFace == null)
        {
            return;
        }

        var emplacement = Level.StartingFace.Id;
        var empValues = new[] { emplacement.X, emplacement.Y, emplacement.Z };
        if (ImGui.InputInt3("Emplacement", ref empValues[0]))
        {
            using (Eddy.History.BeginScope("Edit Gomez Position"))
            {
                Level.StartingFace.Id = new TrileEmplacement(empValues[0], empValues[1], empValues[2]);
            }
        }

        var face = Array.IndexOf(FaceExtensions.NaturalOrder, Level.StartingFace.Face);
        var faces = FaceExtensions.NaturalOrder.Select(fo => fo.ToString()).ToArray();
        if (ImGui.Combo("Face", ref face, faces, faces.Length))
        {
            using (Eddy.History.BeginScope("Edit Gomez Rotation"))
            {
                Level.StartingFace.Face = FaceExtensions.NaturalOrder[face];
            }
        }
    }
}