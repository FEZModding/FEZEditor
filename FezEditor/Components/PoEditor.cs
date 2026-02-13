using FezEditor.Structure;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class PoEditor : EditorComponent
{
    private static readonly string[] ColumnNames = ["Text Id", "Source Text", "Translation Text"];

    public override object Asset => _textStorage;

    private readonly TextStorage _textStorage;

    private readonly List<string[]> _textTable = new();

    private (int Row, int Column) _activeCell = (-1, -1);

    private string _cellText = "";

    private string _newEntryId = "";

    private string _pendingDeleteId = "";

    private Language _selectedLanguage = Language.English;

    private bool _disableTranslationColumn;

    private State _nextState = State.TableView;

    public PoEditor(Game game, string title, TextStorage textStorage) : base(game, title)
    {
        _textStorage = textStorage;
        History.Track(_textStorage);
        History.StateChanged += UpdateTableView;
        UpdateTableView();
    }

    private void UpdateTableView()
    {
        var englishStorage = _textStorage[Language.English.GetId()];
        var selectedStorage = _textStorage[_selectedLanguage.GetId()];

        _textTable.Clear();
        _disableTranslationColumn = _selectedLanguage == Language.English;

        foreach (var id in englishStorage.Keys)
        {
            var source = englishStorage[id];
            if (!selectedStorage.TryGetValue(id, out var translation) || _disableTranslationColumn)
            {
                translation = "";
            }

            _textTable.Add(new[] { id, source, translation });
        }
    }

    public override void Draw()
    {
        #region Toolbar

        ImGuiX.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 8));

        var language = (int)_selectedLanguage;
        var languages = Enum.GetNames<Language>();

        ImGui.SetNextItemWidth(120);
        if (ImGui.Combo("Language", ref language, languages, languages.Length))
        {
            _selectedLanguage = (Language)language;
            UpdateTableView();
        }

        ImGui.SameLine();
        if (ImGui.Button("(+) Add New Entry"))
        {
            _newEntryId = "";
            _nextState = State.AddEntry;
        }

        ImGui.Separator();

        #endregion

        #region Translations Table

        if (ImGui.BeginTable("##PoTable", 3,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY))
        {
            ImGuiX.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(8, 8));
            ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthFixed, 180f);
            ImGui.TableSetupColumn("Source text", ImGuiTableColumnFlags.NoSort);
            ImGui.TableSetupColumn("Translation text", ImGuiTableColumnFlags.NoSort);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();
            ImGui.PopStyleVar();

            var sortSpecs = ImGui.TableGetSortSpecs();
            if (sortSpecs.SpecsDirty)
            {
                _textTable.Sort((a, b) =>
                {
                    var compare = string.Compare(a[0], b[0], StringComparison.Ordinal);
                    if (sortSpecs.Specs.SortDirection == ImGuiSortDirection.Descending)
                    {
                        compare = -compare;
                    }

                    return compare;
                });
                sortSpecs.SpecsDirty = false;
            }

            for (var i = 0; i < _textTable.Count; i++)
            {
                var row = _textTable[i];
                ImGui.TableNextRow(ImGuiTableRowFlags.None, 32f);

                for (var j = 0; j < row.Length; j++)
                {
                    ImGui.TableSetColumnIndex(j);

                    var flags = ImGuiSelectableFlags.None;
                    if (j == row.Length - 1 && _disableTranslationColumn)
                    {
                        flags |= ImGuiSelectableFlags.Disabled;
                    }
                    
                    if (ImGui.Selectable(row[j], false, flags))
                    {
                        _activeCell = (i, j);
                        _cellText = row[j];
                        _nextState = State.MenuPopup;
                    }
                }
            }

            ImGui.EndTable();
        }

        ImGui.PopStyleVar();

        #endregion
        
        #region Menu Popup

        if (_nextState == State.MenuPopup)
        {
            ImGui.OpenPopup("##MenuPopup");
            _nextState = State.TableView;
        }
        
        if (ImGui.BeginPopup("##MenuPopup"))
        {
            if (ImGui.MenuItem("Edit This Cell"))
            {
                _nextState = State.EditCell;
            }
            
            if (ImGui.MenuItem("Add New Entry"))
            {
                _newEntryId = "";
                _nextState = State.AddEntry;
            }

            if (ImGui.MenuItem("Delete This Entry"))
            {
                _pendingDeleteId = _textTable[_activeCell.Row][_activeCell.Column];
                _nextState = State.DeleteEntry;
            }

            ImGui.EndPopup();
        }
        
        #endregion

        #region Edit Text Cell Modal

        if (_nextState == State.EditCell)
        {
            ImGuiX.SetNextWindowCentered();
            ImGui.OpenPopup("##CellEditor");
            _nextState = State.TableView;
        }

        ImGuiX.SetNextWindowSize(new Vector2(512, 0), ImGuiCond.FirstUseEver);
        if (ImGui.BeginPopupModal("##CellEditor",
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar))
        {
            var row = _textTable[_activeCell.Row];
            ImGui.Text($"Editing {ColumnNames[_activeCell.Column]}");
            ImGui.Separator();

            ImGuiX.InputTextMultiline("##edit", ref _cellText, 2048, new Vector2(-1, 240));
            if (ImGui.IsWindowAppearing())
            {
                ImGui.SetKeyboardFocusHere(-1);
            }

            ImGui.Separator();
            if (ImGui.Button("Save"))
            {
                switch (_activeCell.Column)
                {
                    case 0: // Row
                        UpdateId(row[_activeCell.Column], _cellText);
                        break;

                    case 1: // Source
                        UpdateSource(row[0], _cellText);
                        break;

                    case 2: // Translation
                        UpdateTranslation(row[0], _cellText);
                        break;
                }

                _activeCell = (-1, -1);
                ImGui.CloseCurrentPopup();
                UpdateTableView();
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                _activeCell = (-1, -1);
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        #endregion

        #region Add New Entry Modal

        if (_nextState == State.AddEntry)
        {
            ImGuiX.SetNextWindowCentered();
            ImGui.OpenPopup("##AddEntry");
            _nextState = State.TableView;
        }

        ImGuiX.SetNextWindowSize(new Vector2(400, 0), ImGuiCond.FirstUseEver);
        if (ImGui.BeginPopupModal("##AddEntry",
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar))
        {
            ImGui.Text("New Text ID");
            ImGui.Separator();

            ImGuiX.InputTextMultiline("##NewId", ref _newEntryId, 256, new Vector2(-1, 40));
            if (ImGui.IsWindowAppearing())
                ImGui.SetKeyboardFocusHere(-1);

            ImGui.Separator();

            var idExists = _textStorage[Language.English.GetId()].ContainsKey(_newEntryId);
            var idEmpty = string.IsNullOrWhiteSpace(_newEntryId);

            if (idExists)
            {
                ImGuiX.TextColored(new Color(1, 0.3f, 0.3f, 1), "ID already exists.");
            }
            
            if (idEmpty)
            {
                ImGuiX.TextColored(new Color(1, 0.3f, 0.3f, 1), "ID cannot be empty.");
            }

            ImGui.BeginDisabled(idExists || idEmpty);
            if (ImGui.Button("Add"))
            {
                AddEntry(_newEntryId);
                ImGui.CloseCurrentPopup();
                UpdateTableView();
            }

            ImGui.EndDisabled();

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
                ImGui.CloseCurrentPopup();

            ImGui.EndPopup();
        }

        #endregion

        #region Delete Entry Modal

        if (_nextState == State.DeleteEntry)
        {
            ImGuiX.SetNextWindowCentered();
            ImGui.OpenPopup("##DeleteEntry");
            _nextState = State.TableView;
        }

        ImGuiX.SetNextWindowSize(new Vector2(360, 0), ImGuiCond.FirstUseEver);
        if (ImGui.BeginPopupModal("##DeleteEntry",
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar))
        {
            ImGui.Text($"Delete \"{_pendingDeleteId}\" from all languages?");
            ImGui.Separator();

            if (ImGui.Button("Delete"))
            {
                DeleteEntry(_pendingDeleteId);
                _pendingDeleteId = "";
                ImGui.CloseCurrentPopup();
                UpdateTableView();
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                _pendingDeleteId = "";
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        #endregion
    }

    private void UpdateId(string id, string newId)
    {
        using (History.BeginScope("Update id of text"))
        {
            foreach (var storage in _textStorage.Values)
            {
                if (storage.Remove(id, out var text))
                {
                    storage.Add(newId, text);
                }
            }
        }
    }

    private void UpdateSource(string id, string source)
    {
        using (History.BeginScope("Update source of text"))
        {
            var englishStorage = _textStorage[Language.English.GetId()];
            englishStorage[id] = NormalizeLineEndings(source);
        }
    }

    private void UpdateTranslation(string id, string translation)
    {
        using (History.BeginScope("Update translation of text"))
        {
            var languageStorage = _textStorage[_selectedLanguage.GetId()];
            languageStorage[id] = NormalizeLineEndings(translation);
        }
    }
    
    private void AddEntry(string id)
    {
        using (History.BeginScope("Add text entry"))
        {
            foreach (var storage in _textStorage.Values)
            {
                storage.TryAdd(id, "");
            }
        }
    }

    private void DeleteEntry(string id)
    {
        using (History.BeginScope("Delete text entry"))
        {
            foreach (var storage in _textStorage.Values)
            {
                storage.Remove(id);
            }
        }
    }
    
    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\n", "\r\n");
    }

    private enum State
    {
        TableView,
        MenuPopup,
        EditCell,
        AddEntry,
        DeleteEntry
    }
}