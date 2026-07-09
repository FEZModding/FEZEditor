using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class MainLayout : DrawableGameComponent
{
    private const float DefaultLeftPaneWidth = 250f;

    public bool ShowFileBrowser { get; set; } = true;

    private readonly EditorService _editorService;

    private readonly StatusService _statusService;

    private readonly AppStorageService _storageService;

    private readonly FileBrowser _fileBrowser;

    private readonly ConfirmWindow _confirm;

    private bool _loadNextUpdate;

    private bool _confirmPending;

    public MainLayout(Game game) : base(game)
    {
        _editorService = Game.GetService<EditorService>();
        _statusService = Game.GetService<StatusService>();
        _storageService = Game.GetService<AppStorageService>();
        _fileBrowser = Game.GetComponent<FileBrowser>();
        Game.AddComponent(_confirm = new ConfirmWindow(game));
        DrawOrder = -1;
    }

    protected override void Dispose(bool disposing)
    {
        _editorService.CloseAllEditors();
        _editorService.FlushPendingCloses();
        Game.RemoveComponent(_confirm);
    }

    public override void Update(GameTime gameTime)
    {
        _editorService.Update(gameTime);
    }

    public override void Draw(GameTime gameTime)
    {
        // Flush loading editors on next frame
        if (_loadNextUpdate)
        {
            _loadNextUpdate = false;
            _editorService.FlushPendingLoads();
        }

        // Clear previously closed editors
        _editorService.FlushPendingCloses();

        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(viewport.WorkSize, ImGuiCond.Always);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

        if (ImGui.Begin("##MainLayout",
                ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBringToFrontOnFocus |
                ImGuiWindowFlags.NoSavedSettings))
        {
            // Region: Left pane + Right pane
            {
                // Left pane - File Browser (resizable horizontally)
                if (ShowFileBrowser)
                {
                    ImGuiX.BeginChild("LeftPane", new Vector2(DefaultLeftPaneWidth, 0),
                        ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeX);
                    _fileBrowser.Draw();
                    ImGui.EndChild();
                    ImGui.SameLine();
                }

                // Right pane - Editor tabs + Status bar
                ImGuiX.BeginChild("RightPane", Vector2.Zero);
                {
                    var statusBarHeight = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y + 1;

                    ImGuiX.BeginChild("EditorArea", new Vector2(0, -statusBarHeight));
                    if (_editorService.Editors.Any())
                    {
                        if (_editorService.Editors.FirstOrDefault() is WelcomeSplash welcome)
                        {
                            DrawEditor(welcome);
                        }
                        else if (ImGui.BeginTabBar("##EditorTabs"))
                        {
                            foreach (var editor in _editorService.Editors.ToArray())
                            {
                                var title = editor.Title;
                                if (_editorService.IsEditorPathReadonly(editor))
                                {
                                    title = $"{Lucide.LockKeyhole} {title}";
                                }

                                if (_editorService.IsEditorDeleted(editor))
                                {
                                    title = $"{Lucide.Trash2} {title}";
                                }

                                var tabFlags = ImGuiTabItemFlags.NoAssumedClosure;
                                if (_editorService.ShouldFocusEditor(editor))
                                {
                                    tabFlags |= ImGuiTabItemFlags.SetSelected;
                                }

                                if (_editorService.HasEditorUnsavedChanges(editor))
                                {
                                    tabFlags |= ImGuiTabItemFlags.UnsavedDocument;
                                }

                                var tabLabel = title + "###" + editor.Title;
                                var isOpen = true;
                                if (ImGui.BeginTabItem(tabLabel, ref isOpen, tabFlags))
                                {
                                    DrawEditor(editor);
                                    ImGui.EndTabItem();
                                }

                                if (!isOpen)
                                {
                                    SaveAndCloseEditor(editor);
                                }
                            }

                            ImGui.EndTabBar();
                        }
                    }
                    else
                    {
                        const string text = $"{Lucide.ArrowLeft} Open a file from File Browser";
                        ImGuiX.SetTextCentered(text);
                        ImGui.Text(text);
                    }

                    ImGui.EndChild();

                    DrawStatusBar();
                }

                ImGui.EndChild();
            }
        }

        ImGui.End();
        ImGui.PopStyleVar(2);
    }

    private void DrawEditor(EditorComponent editor)
    {
        if (!_editorService.IsEditorLoading(editor))
        {
            _editorService.MarkEditorActive(editor);
            editor.Draw();
            return;
        }

        const string text = "Loading...";
        ImGuiX.SetTextCentered(text);
        ImGui.Text(text);
        _loadNextUpdate = true;
    }

    private void DrawStatusBar()
    {
        var activity = _statusService.CurrentActivity;
        var hints = _statusService.Hints;
        _statusService.AddRightHints(
            ("HAT", GetHatStatusText()),
            ("", $"{FezEditor.Version} ({FezEditor.Commit})")
        );
        var rightHints = _statusService.RightHints;
        var hintText = string.Join(" | ", hints.Select(hint => $"{hint.Binding} - {hint.Label}"));
        var statusText = activity == null || string.IsNullOrEmpty(hintText)
            ? activity?.Text ?? hintText
            : $"{activity.Text} | {hintText}";

        ImGui.Separator();
        if (ImGuiX.BeginChild("StatusBar", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar))
        {
            const float rightItemSpacing = 16f;
            var rightItems = BuildRightStatusItems(rightHints, rightItemSpacing);
            var rightContentX = rightItems.Count > 0
                ? rightItems.Min(item => item.X)
                : ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X;
            var progressWidth = activity?.Progress != null ? 160f : 0f;
            var progressSpacing = progressWidth > 0 ? 8f : 0f;
            var textWidth = rightContentX - ImGui.GetCursorPosX() - progressWidth - progressSpacing - 16f;
            var visibleStatus = Ellipsize(statusText, textWidth);
            var drewLeftContent = false;

            if (!string.IsNullOrEmpty(visibleStatus))
            {
                ImGui.TextUnformatted(visibleStatus);
                drewLeftContent = true;
            }

            if (activity?.Progress is { } progress)
            {
                if (drewLeftContent)
                {
                    ImGui.SameLine(0, progressSpacing);
                }

                ImGuiX.ProgressBar(progress, new Vector2(progressWidth, 0), $"{progress * 100:F0}%");
                drewLeftContent = true;
            }

            if (drewLeftContent)
            {
                ImGui.SameLine();
            }

            DrawRightStatusItems(rightItems);
        }

        ImGui.EndChild();
    }

    private void SaveAndCloseEditor(EditorComponent editor)
    {
        if (!_editorService.HasEditorUnsavedChanges(editor) || _editorService.IsEditorPathReadonly(editor))
        {
            _editorService.CloseEditor(editor);
            return;
        }

        if (_confirmPending)
        {
            return;
        }

        _confirmPending = true;
        _confirm.Title = "Before closing the editor...";
        _confirm.Text = "Save current changes?";
        _confirm.Confirmed = () =>
        {
            _editorService.SaveEditorChanges(editor);
            _editorService.CloseEditor(editor);
        };
        _confirm.Denied = () =>
        {
            _editorService.CloseEditor(editor);
        };
        _confirm.Closed = () =>
        {
            _confirmPending = false;
        };
    }

    private string GetHatStatusText()
    {
        if (string.IsNullOrWhiteSpace(_storageService.HatLauncherPath))
        {
            return "Not configured";
        }

        return File.Exists(_storageService.HatLauncherPath)
            ? "Available"
            : "Missing";
    }

    private static List<RightStatusItem> BuildRightStatusItems(
        IReadOnlyList<(string Binding, string Label)> hints,
        float spacing)
    {
        var items = new List<RightStatusItem>();
        var maxItemWidth = Math.Min(480f, ImGui.GetWindowWidth() * 0.35f);
        var cursorX = ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X;

        for (var i = hints.Count - 1; i >= 0; i--)
        {
            var text = FormatStatusHint(hints[i]);
            var visible = Ellipsize(text, maxItemWidth);
            if (string.IsNullOrEmpty(visible))
            {
                continue;
            }

            var width = ImGui.CalcTextSize(visible).X;
            cursorX -= width;
            items.Add(new RightStatusItem(cursorX, visible, text));
            cursorX -= spacing;
        }

        return items;
    }

    private static void DrawRightStatusItems(List<RightStatusItem> items)
    {
        var cursorY = ImGui.GetCursorPosY();
        foreach (var item in items)
        {
            ImGui.SetCursorPos(new NVector2(item.X, cursorY));
            ImGui.TextDisabled(item.VisibleText);
            if (ImGui.IsItemHovered() && item.VisibleText != item.FullText)
            {
                ImGui.SetTooltip(item.FullText);
            }
        }
    }

    private static string FormatStatusHint((string Binding, string Label) hint)
    {
        return string.IsNullOrEmpty(hint.Binding)
            ? hint.Label
            : $"{hint.Binding} - {hint.Label}";
    }

    private static string Ellipsize(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0f)
        {
            return string.Empty;
        }

        if (ImGui.CalcTextSize(text).X <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        if (ImGui.CalcTextSize(ellipsis).X > maxWidth)
        {
            return string.Empty;
        }

        var low = 0;
        var high = text.Length;
        while (low < high)
        {
            var length = (low + high + 1) / 2;
            if (ImGui.CalcTextSize(text[..length] + ellipsis).X <= maxWidth)
            {
                low = length;
            }
            else
            {
                high = length - 1;
            }
        }

        return text[..low].TrimEnd() + ellipsis;
    }

    private readonly record struct RightStatusItem(float X, string VisibleText, string FullText);
}