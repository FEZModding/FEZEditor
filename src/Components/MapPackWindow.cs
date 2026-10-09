using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.MapTree;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Serilog;

namespace FezEditor.Components;

public class MapPackWindow : DrawableGameComponent
{
    private const string PopupId = "###WorldMapPack";

    private readonly ResourceService _resources;

    private readonly EditorService _editors;

    private readonly InputService _input;

    private readonly AssetPickWindow _assetPicker;

    private WorldMetadata? _world;

    private string? _filePath;

    private bool _pendingOpen;

    private bool _open;

    private bool _closePending;

    private Action? _afterClose;

    private string? _error;

    public bool IsOpen => _open || _pendingOpen;

    private WorldMetadata World => _world!;

    public MapPackWindow(Game game) : base(game)
    {
        _resources = game.GetService<ResourceService>();
        _editors = game.GetService<EditorService>();
        _input = game.GetService<InputService>();
        _assetPicker = new AssetPickWindow(game);
        _resources.ProviderReset += OnProviderReset;
    }

    public void Show()
    {
        if (IsOpen || _resources.ModResolution == null || _resources.IsReadonly)
        {
            return;
        }

        _error = null;
        try
        {
            _filePath = Path.Combine(_resources.ModResolution.ModRootDirectory.FullName, WorldMetadata.FileName);
            if (File.Exists(_filePath))
            {
                using var stream = File.OpenRead(_filePath);
                _world = WorldMetadata.Read(stream);
            }
            else
            {
                _world = new WorldMetadata();
            }

            World.DisplayName ??= string.Empty;
            World.Description ??= string.Empty;
            World.StartingLevel = NormalizeAssetPath(World.StartingLevel, true);
            World.MapTree = NormalizeAssetPath(World.MapTree);
            World.Thumbnail = NormalizeAssetPath(World.Thumbnail);
            World.StartingSaveFields ??= new List<WorldMetadata.SaveFieldDefinition>();
            World.AlwaysBlackHoleLevels = (World.AlwaysBlackHoleLevels ?? new List<string>())
                .Select(path => NormalizeAssetPath(path, true)).ToList();
            World.DotCensorship ??= new List<string>();
        }
        catch (Exception ex)
        {
            _world = null;
            Log.Error(ex, "Could not open World.xml");
            _error = $"Could not open World.xml: {ex.InnerException?.Message ?? ex.Message}";
        }

        _pendingOpen = true;
    }

    public void RequestClose(Action? afterClose = null)
    {
        _afterClose = afterClose;
        _closePending = true;
    }

    public override void Draw(GameTime gameTime)
    {
        const string title = "World Map Pack" + PopupId;
        if (_pendingOpen)
        {
            ImGui.OpenPopup(title);
            _open = true;
            _pendingOpen = false;
        }

        if (!_open)
        {
            return;
        }

        var viewport = ImGui.GetMainViewport();
        ImGuiX.SetNextWindowSize(
            new Vector2(Math.Min(680, viewport.WorkSize.X - 32), Math.Min(760, viewport.WorkSize.Y - 32)),
            ImGuiCond.Appearing);
        ImGuiX.SetNextWindowCentered(ImGuiCond.Appearing);
        if (!ImGui.BeginPopupModal(title, ImGuiWindowFlags.NoSavedSettings))
        {
            return;
        }

        if (_error != null)
        {
            ImGui.TextWrapped(_error);
        }

        if (_world != null)
        {
            if (ImGuiX.BeginChild("MetadataFields", new Vector2(0, -ImGui.GetFrameHeightWithSpacing() - 8)))
            {
                DrawMetadata();
            }

            ImGui.EndChild();

            if (ImGui.Button("Save"))
            {
                Save();
            }

            ImGui.SameLine();
        }

        if (ImGui.Button("Close"))
        {
            RequestClose();
        }

        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                RequestClose();
            }

            if (_world != null)
            {
                if (_input.IsActionJustPressed(InputActions.UiSave))
                {
                    Save();
                }
                else if (_input.IsActionJustPressed(InputActions.UiSaveAll) && Save())
                {
                    foreach (var editor in _editors.Editors)
                    {
                        _editors.SaveEditorChanges(editor);
                    }
                }
            }
        }

        _assetPicker.Draw(gameTime);
        if (_closePending)
        {
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
        if (_closePending)
        {
            var afterClose = _afterClose;
            Reset();
            afterClose?.Invoke();
        }
    }

    private bool Save()
    {
        if (_world == null || _filePath == null)
        {
            return false;
        }

        try
        {
            // Serialize before touching disk and replace only a complete XML file.
            using var buffer = new MemoryStream();
            World.Write(buffer);
            var temporaryPath = _filePath + $".{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllBytes(temporaryPath, buffer.ToArray());
                File.Move(temporaryPath, _filePath, true);
            }
            finally
            {
                File.Delete(temporaryPath);
            }

            _error = null;
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not save World Map Pack");
            _error = "Could not save World Map Pack: " + ex.Message;
            return false;
        }
    }

    private void DrawMetadata()
    {
        var saveFields = World.StartingSaveFields ??= new();
        var blackHoleLevels = World.AlwaysBlackHoleLevels ??= new();
        var dotCensorship = World.DotCensorship ??= new();
        EditText("Display Name", () => World.DisplayName, value => World.DisplayName = value);
        EditText("Description", () => World.Description, value => World.Description = value, true);
        EditReference("Starting Level", () => World.StartingLevel, value => World.StartingLevel = value,
            ReferenceKind.Level);
        EditReference("Map Tree", () => World.MapTree, value => World.MapTree = value, ReferenceKind.MapTree);
        EditReference("Thumbnail", () => World.Thumbnail, value => World.Thumbnail = value, ReferenceKind.Texture);
        DrawList("Starting Save Fields", () => saveFields,
            () => new WorldMetadata.SaveFieldDefinition(), index =>
            {
                EditText("Name", () => saveFields[index].Name,
                    value => saveFields[index].Name = value);
                EditText("Value", () => saveFields[index].Value,
                    value => saveFields[index].Value = value);
            });
        DrawList("Always-active Black-hole Levels", () => blackHoleLevels,
            () => string.Empty, index => EditReference("Level", () => blackHoleLevels[index],
                value => blackHoleLevels[index] = value, ReferenceKind.Level));
        DrawList("Suppressed Dot Dialogue", () => dotCensorship,
            () => string.Empty, index => EditText("Identifier", () => dotCensorship[index],
                value => dotCensorship[index] = value));
    }

    private static void EditText(
        string label,
        Func<string?> get,
        Action<string> set,
        bool multiline = false,
        Func<string, string>? normalize = null)
    {
        ImGui.TextUnformatted(label);
        ImGui.SetNextItemWidth(-1);
        var value = get() ?? string.Empty;
        var changed = multiline
            ? ImGui.InputTextMultiline("##" + label, ref value, 16384, new System.Numerics.Vector2(-1, 90))
            : ImGui.InputText("##" + label, ref value, 4096);

        if (changed)
        {
            set(normalize?.Invoke(value) ?? value);
        }
    }

    private void EditReference(string label, Func<string?> get, Action<string> set, ReferenceKind kind)
    {
        EditText(label, get, set,
            normalize: value => NormalizeAssetPath(value, kind == ReferenceKind.Level));

        if (ImGui.Button($"{Lucide.FolderOpen} Pick Asset##{label}"))
        {
            _assetPicker.Title = "Select " + label;
            _assetPicker.Text = "Pick an asset for " + label + ":";
            _assetPicker.RootPath = kind == ReferenceKind.Level ? "Levels/" : string.Empty;
            _assetPicker.MissingAssetsText = "(no assets found)";
            _assetPicker.Recursive = true;
            _assetPicker.Filter = path => MatchesReference(path, kind);
            _assetPicker.Accepted = path => set(NormalizeAssetPath(path, kind == ReferenceKind.Level));
            _assetPicker.ForceToShow();
        }

        var path = NormalizeAssetPath(get(), kind == ReferenceKind.Level);
        if (string.IsNullOrWhiteSpace(path))
        {
            ImGui.TextWrapped($"{Lucide.Info} {label} is empty. You can still save.");
        }
        else if (!_resources.Exists(kind == ReferenceKind.Level ? "Levels/" + path : path))
        {
            ImGui.TextWrapped($"{Lucide.Info} Asset not found: {path}. You can still save.");
        }

        ImGui.Spacing();
    }

    private bool MatchesReference(string path, ReferenceKind kind)
    {
        var extension = _resources.GetExtension(path).ToLowerInvariant();
        return kind switch
        {
            ReferenceKind.Level => extension is ".fezlvl.json" or ".xnb",
            ReferenceKind.MapTree => extension == ".fezmap.json" || (extension == ".xnb" && _resources.TryLoad<MapTree>(path, out _)),
            ReferenceKind.Texture => extension == ".png" || (extension == ".xnb" && _resources.TryLoad<RTexture2D>(path, out _)),
            _ => false
        };
    }

    private static void DrawList<T>(string label, Func<List<T>> get, Func<T> create, Action<int> drawItem)
    {
        ImGui.SeparatorText(label);
        ImGui.PushID(label);
        for (var index = 0; index < get().Count; index++)
        {
            ImGui.PushID(index);
            drawItem(index);
            var remove = ImGui.Button($"{Lucide.X} Remove");
            if (remove)
            {
                get().RemoveAt(index);
            }

            ImGui.Separator();
            ImGui.PopID();
            if (remove)
            {
                break;
            }
        }

        if (ImGui.Button($"{Lucide.Plus} Add"))
        {
            get().Add(create());
        }

        ImGui.PopID();
    }

    private void OnProviderReset()
    {
        _closePending = IsOpen;
    }

    private void Reset()
    {
        _world = null;
        _filePath = null;
        _open = _pendingOpen = _closePending = false;
        _afterClose = null;
    }

    protected override void Dispose(bool disposing)
    {
        _resources.ProviderReset -= OnProviderReset;
        Reset();
        _assetPicker.Dispose();
        base.Dispose(disposing);
    }

    private static string NormalizeAssetPath(string? path, bool level = false)
    {
        var normalized = (path ?? string.Empty).Trim().Replace('\\', '/');
        const string references = "References/";
        if (normalized.StartsWith(references, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[references.Length..];
        }

        if (level && normalized.StartsWith("Levels/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["Levels/".Length..];
        }

        string[] extensions = [".fezlvl.json", ".fezmap.json", ".xnb", ".png", ".gif"];
        foreach (var extension in extensions)
        {
            if (normalized.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return normalized[..^extension.Length];
            }
        }

        return normalized;
    }

    private enum ReferenceKind
    {
        Level,
        MapTree,
        Texture
    }
}
