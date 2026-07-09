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

    private readonly FileBrowser _fileBrowser;

    private readonly StatusBar _statusBar;

    private readonly ConfirmWindow _confirm;

    private bool _loadNextUpdate;

    private bool _confirmPending;

    public MainLayout(Game game) : base(game)
    {
        _editorService = Game.GetService<EditorService>();
        _fileBrowser = Game.GetComponent<FileBrowser>();
        _statusBar = Game.GetComponent<StatusBar>();
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
                    _statusBar.Draw();
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
}