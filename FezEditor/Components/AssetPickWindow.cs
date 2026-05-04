using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class AssetPickWindow : DrawableGameComponent
{
    public Dirty<string> Title { get; set; } = new("");

    public Dirty<string> Text { get; set; } = new("Edit the value");

    public Dirty<string> AcceptButtonText { get; set; } = new("Accept");

    public Dirty<string> CancelButtonText { get; set; } = new("Cancel");

    public Dirty<string> RootPath { get; set; } = new("");
    public Dirty<string> MissingAssetsText { get; set; } = new("");

    public Action<string>? Accepted { get; set; }

    public Action? Canceled { get; set; }

    private bool _isDirty;

    private string[] _cachedAssetPaths = Array.Empty<string>();
    private int _selectedAssetIndex = -1;

    private readonly int _popupId = Random.Shared.Next();

    private readonly ResourceService _resourceService;

    public AssetPickWindow(Game game) : base(game)
    {
        _resourceService = game.GetService<ResourceService>();
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
               RootPath.IsDirty ||
               MissingAssetsText.IsDirty ||
               AcceptButtonText.IsDirty ||
               CancelButtonText.IsDirty;
    }

    private void Clear()
    {
        _isDirty = false;
        Title = Title.Clean();
        Text = Text.Clean();
        RootPath = RootPath.Clean();
        MissingAssetsText = MissingAssetsText.Clean();
        AcceptButtonText = AcceptButtonText.Clean();
        CancelButtonText = CancelButtonText.Clean();
    }

    public override void Draw(GameTime gameTime)
    {
        var strId = $"{Title.Value}##AssetPickWindow_{_popupId}";
        if (IsDirty())
        {
            if (string.IsNullOrEmpty(Text))
            {
                throw new ArgumentException("Dialog text is empty");
            }

            RecacheAssetPaths();
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

            var listId = $"##AssetPickWindow_{_popupId}_ListBox";
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

            if (_cachedAssetPaths.Length == 0)
            {
                ImGui.BeginDisabled();
                ImGui.ListBox(listId, ref _selectedAssetIndex, [MissingAssetsText], 1);
                ImGui.EndDisabled();
            }
            else
            {
                ImGui.ListBox(listId, ref _selectedAssetIndex,
                    _cachedAssetPaths, _cachedAssetPaths.Length, Math.Min(_cachedAssetPaths.Length, 10));
            }

            if (ImGui.IsWindowAppearing())
            {
                ImGui.SetKeyboardFocusHere(-1);
            }

            ImGui.Separator();
            ImGui.BeginDisabled(_selectedAssetIndex < 0);

            if (ImGui.Button(AcceptButtonText))
            {
                if (_cachedAssetPaths.Length > 0 && _selectedAssetIndex > 0)
                {
                    var assetPath = $"{RootPath.Value}{_cachedAssetPaths[_selectedAssetIndex]}";
                    Accepted?.Invoke(assetPath);
                }
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

    private void RecacheAssetPaths()
    {
        if (string.IsNullOrEmpty(RootPath))
        {
            throw new ArgumentException("Empty root path in asset pick window");
        }

        _cachedAssetPaths = _resourceService.Files
            .Where(path => path.StartsWith(RootPath, StringComparison.OrdinalIgnoreCase))
            .Select(path => path[RootPath.Value.Length..])
            .ToArray();
        _selectedAssetIndex = -1;
    }
}