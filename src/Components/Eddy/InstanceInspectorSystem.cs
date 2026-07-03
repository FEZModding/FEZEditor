using ImGuiNET;

namespace FezEditor.Components.Eddy;

public class InstanceInspectorSystem : EddySystem
{
    public override void Draw()
    {
        if (!Eddy.ShowProperties)
        {
            return;
        }

        const ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize |
                                       ImGuiWindowFlags.NoCollapse;

        var isOpen = Eddy.ShowProperties;
        if (ImGui.Begin("Properties", ref isOpen, flags))
        {
            var instances = new HashSet<InstanceId>();

            if (Eddy.Selected is SelectionState.Trile { Selected.Count: > 0 } ts)
            {
                foreach (var te in ts.Selected)
                {
                    instances.Add(new InstanceId.Trile(te));
                }
            }
            else if (Eddy.Selected is SelectionState.TrileGroup { Selected.Count: 1 } tg)
            {
                instances.Add(new InstanceId.TrileGroup(tg.Selected.Single()));
            }
            else if (Eddy.Selected is SelectionState.Instance { Selected.Count: 1 } i)
            {
                instances.Add(i.Selected.Single());
            }
            else if (Eddy.Selected is SelectionState.Path p)
            {
                instances.Add(p.Selected);
            }
            else
            {
                instances.Add(new InstanceId.Level());
            }

            Eddy.Inspect(instances);
            ImGui.End();
        }

        if (!isOpen)
        {
            Eddy.ShowProperties = false;
        }
    }
}