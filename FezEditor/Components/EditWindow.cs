using FezEditor.Structure;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class EditWindow : DrawableGameComponent
{
    public delegate bool EditValueDelegate();

    public Dirty<string> Title { get; set; } = new("");

    public Dirty<string> Text { get; set; } = new("Edit the value");

    public Dirty<string> AcceptButtonText { get; set; } = new("Accept");

    public Dirty<string> CancelButtonText { get; set; } = new("Cancel");

    public EditValueDelegate? EditValue { get; set; }

    public Action? Accepted { get; set; }

    public Action? Canceled { get; set; }

    private bool _isDirty;

    private readonly int _popupId = Random.Shared.Next();

    public EditWindow(Game game) : base(game)
    {
    }

    public void ForceToShow()
    {
        _isDirty = true;
    }

    private bool IsDirty()
    {
        return _isDirty ||
               Title.IsDirty ||
               Text.IsDirty ||
               AcceptButtonText.IsDirty ||
               CancelButtonText.IsDirty;
    }

    private void Clear()
    {
        _isDirty = false;
        Title = Title.Clean();
        Text = Text.Clean();
        AcceptButtonText = AcceptButtonText.Clean();
        CancelButtonText = CancelButtonText.Clean();
    }

    public override void Draw(GameTime gameTime)
    {
        var strId = $"{Title.Value}##EditWindow_{_popupId}";
        if (IsDirty())
        {
            if (string.IsNullOrEmpty(Text))
            {
                throw new ArgumentException("Dialog text is empty");
            }

            ImGuiX.SetNextWindowCentered();
            ImGui.OpenPopup(strId);
            Clear();
        }

        var flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;
        if (string.IsNullOrEmpty(Title))
        {
            flags |= ImGuiWindowFlags.NoTitleBar;
        }

        ImGuiX.SetNextWindowSize(new Vector2(320, 0));

        if (ImGui.BeginPopupModal(strId, flags))
        {
            ImGui.Text(Text);
            ImGui.Separator();

            var valid = EditValue?.Invoke() ?? false;
            if (ImGui.IsWindowAppearing())
            {
                ImGui.SetKeyboardFocusHere(-1);
            }

            ImGui.Separator();
            ImGui.BeginDisabled(!valid);

            if (ImGui.Button(AcceptButtonText))
            {
                Accepted?.Invoke();
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndDisabled();
            ImGui.SameLine();

            if (ImGui.Button(CancelButtonText))
            {
                Canceled?.Invoke();
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }
}