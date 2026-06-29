using System.Globalization;
using System.Text.Json;
using FezEditor.Scripting;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level.Scripting;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public class ScriptBrowserSystem : EddySystem
{
    private const int Columns = 4;

    private const float RowHeight = 48f;

    private const float TriggerFormHeight = 188f;

    private const float ConditionFormHeight = 188f;

    private const float ActionFormHeight = 188f;

    private ConfirmWindow? _confirm;

    private int _id = -1;

    private Script? _script;

    private int _triggerIndex = -1;

    private int _conditionIndex = -1;

    private int _actionIndex = -1;

    private Entity? _pickTarget;

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_confirm != null)
        {
            Game.RemoveComponent(_confirm);
            _confirm = null;
        }
    }

    public override void Draw()
    {
        if (!Eddy.ShowScriptBrowser)
        {
            if (_confirm != null)
            {
                Game.RemoveComponent(_confirm);
                _confirm = null;
            }

            return;
        }

        if (_confirm == null)
        {
            Game.AddComponent(_confirm = new ConfirmWindow(Game));
        }

        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoCollapse;
        ImGuiX.SetNextWindowSize(new Vector2(500, 400), ImGuiCond.FirstUseEver);

        var isOpen = Eddy.ShowScriptBrowser;
        if (ImGui.Begin("Script Browser", ref isOpen, flags))
        {
            PollPickingState();
            DrawTable();
            DrawEditorWindow();
            ImGui.End();
        }

        if (!isOpen)
        {
            Eddy.ShowScriptBrowser = false;
        }
    }

    private void PollPickingState()
    {
        if (_pickTarget == null || Eddy.Picked is not PickingState.Picked picked)
        {
            return;
        }

        var canPick = _pickTarget.Type switch
        {
            "ArtObject" => picked.Instance is InstanceId.ArtObject,
            "Plane" => picked.Instance is InstanceId.BackgroundPlane,
            "Npc" => picked.Instance is InstanceId.NonPlayableCharacter,
            "Volume" => picked.Instance is InstanceId.Volume,
            "Path" => picked.Instance is InstanceId.Path or InstanceId.GroupPath,
            "Group" or "RotatingGroup" or "SuckBlock" or "Switch" or "SpinBlock"
                => picked.Instance is InstanceId.TrileGroup,
            _ => false
        };

        if (!canPick)
        {
            return;
        }

        using (Eddy.History.BeginScope("Pick Entity Identifier"))
        {
            _pickTarget.Identifier = picked.Instance.GetId();
        }

        _pickTarget = null;
        Eddy.Picked = new PickingState.None();
    }

    private void DrawTable()
    {
        const ImGuiTableFlags tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders |
                                           ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY;
        var tableSize = new NVector2(0, ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing());
        if (ImGui.BeginTable("##ScriptList", Columns, tableFlags, tableSize))
        {
            ImGuiX.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(8, 8));
            ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Triggers", ImGuiTableColumnFlags.NoSort);
            ImGui.TableSetupColumn("Conditions", ImGuiTableColumnFlags.NoSort);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.NoSort);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();
            ImGui.PopStyleVar();

            foreach (var (id, script) in Level.Scripts.ToArray())
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, RowHeight);
                if (ImGui.IsPopupOpen($"##ScriptCtx{id}"))
                {
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(ImGuiCol.HeaderActive));
                }

                ImGui.TableSetColumnIndex(0);
                ImGui.Selectable($"{id}##sel{id}", false, ImGuiSelectableFlags.SpanAllColumns,
                    new NVector2(0, RowHeight));
                if (ImGui.BeginPopupContextItem($"##ScriptCtx{id}"))
                {
                    if (ImGui.MenuItem($"{Lucide.Plus} Add New"))
                    {
                        CreateNewScript();
                    }

                    if (ImGui.MenuItem($"{Lucide.Pencil} Edit"))
                    {
                        (_id, _script) = (id, script);
                        _actionIndex = -1;
                        _triggerIndex = -1;
                        _conditionIndex = -1;
                    }

                    if (ImGui.MenuItem($"{Lucide.Copy} Clone"))
                    {
                        var nextId = Level.Scripts.Keys.DefaultIfEmpty(-1).Max() + 1;
                        var clone = Clone(script);
                        Level.Scripts.Add(nextId, clone);
                    }

                    if (ImGui.MenuItem($"{Lucide.X} Delete"))
                    {
                        _confirm!.Title = "Script Browser";
                        _confirm.Text = "Delete this script?";
                        _confirm.ConfirmButtonText = "Yes";
                        _confirm.DenyButtonText = "No";
                        _confirm.Confirmed = () => Level.Scripts.Remove(id);
                        _confirm.Denied = null;
                    }

                    ImGui.EndPopup();
                }

                ImGui.TableSetColumnIndex(1);
                ImGui.Text(TruncateLines(script.Triggers.EmptyIfNull().Select(st => st.Stringify())));

                ImGui.TableSetColumnIndex(2);
                ImGui.Text(TruncateLines(script.Conditions.EmptyIfNull().Select(sc => sc.Stringify())));

                ImGui.TableSetColumnIndex(3);
                ImGui.Text(TruncateLines(script.Actions.EmptyIfNull().Select(sa => sa.Stringify())));
            }

            ImGui.EndTable();
        }

        if (ImGui.Button($"{Lucide.Plus} Add new"))
        {
            CreateNewScript();
        }
    }

    private static string TruncateLines(IEnumerable<string> lines, int max = 3)
    {
        var list = lines.ToList();
        if (list.Count <= max)
        {
            return string.Join("\n", list);
        }

        var head = list.Take(max / 2);
        var tail = list.TakeLast(max / 2);
        return string.Join("\n", head) + "\n...\n" + string.Join("\n", tail);
    }

    private void CreateNewScript()
    {
        _id = Level.Scripts.Keys.DefaultIfEmpty(-1).Max() + 1;
        Level.Scripts.Add(_id, _script = new Script());
        _actionIndex = -1;
        _triggerIndex = -1;
        _conditionIndex = -1;
    }

    private void DrawEditorWindow()
    {
        if (_script == null)
        {
            return;
        }

        var title = $"Edit {_script.Name} ({_id}) script##ScriptEditor";
        var open = true;

        ImGuiX.SetNextWindowSize(new Vector2(960, 640), ImGuiCond.FirstUseEver);
        if (ImGui.Begin(title, ref open, ImGuiWindowFlags.NoCollapse))
        {
            DrawScriptHeader();

            var availSize = ImGui.GetContentRegionAvail();
            var width = availSize.X / 3f;

            if (ImGuiX.BeginChild("##Triggers", new Vector2(width, 0), ImGuiChildFlags.Border))
            {
                DrawTriggers();
                ImGui.EndChild();
            }

            ImGui.SameLine();

            if (ImGuiX.BeginChild("##Conditions", new Vector2(width, 0), ImGuiChildFlags.Border))
            {
                DrawConditions();
                ImGui.EndChild();
            }

            ImGui.SameLine();

            if (ImGuiX.BeginChild("##Actions", Vector2.Zero, ImGuiChildFlags.Border))
            {
                DrawActions();
                ImGui.EndChild();
            }

            ImGui.End();
        }

        if (!open)
        {
            _script = null;
            _id = -1;
        }
    }

    private void DrawScriptHeader()
    {
        ImGui.TextDisabled($"#{_id}");
        ImGui.SameLine();

        ImGui.SetNextItemWidth(160f);
        var name = _script!.Name;
        if (ImGui.InputText("##Name", ref name, 255))
        {
            using (Eddy.History.BeginScope("Edit Script Name"))
            {
                _script.Name = name;
            }
        }

        ImGui.SameLine();

        var hasTimeout = _script.Timeout.HasValue;
        if (ImGui.Checkbox("Timeout##hdr", ref hasTimeout))
        {
            using (Eddy.History.BeginScope("Edit Script Timeout Flag"))
            {
                _script.Timeout = hasTimeout ? TimeSpan.Zero : null;
            }
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(!_script.Timeout.HasValue);
        ImGui.SetNextItemWidth(64f);
        var timeout = (float)(_script.Timeout?.TotalSeconds ?? 0d);
        if (ImGui.InputFloat("s##timeout", ref timeout, 0f, 0f, "%.1f"))
        {
            using (Eddy.History.BeginScope("Edit Script Timeout Value"))
            {
                _script.Timeout = TimeSpan.FromSeconds(timeout);
            }
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        var oneTime = _script.OneTime;
        if (ImGui.Checkbox("One-Time##hdr", ref oneTime))
        {
            using (Eddy.History.BeginScope("Edit Script OneTime"))
            {
                _script.OneTime = oneTime;
            }
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(!oneTime);
        var levelWideOnly = _script.LevelWideOneTime;
        if (ImGui.Checkbox("Level-Wide##hdr", ref levelWideOnly))
        {
            using (Eddy.History.BeginScope("Edit Script LevelWideOneTime"))
            {
                _script.LevelWideOneTime = levelWideOnly;
            }
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        var disabled = _script.Disabled;
        if (ImGui.Checkbox("Disabled##hdr", ref disabled))
        {
            using (Eddy.History.BeginScope("Edit Script Disabled"))
            {
                _script.Disabled = disabled;
            }
        }

        ImGui.SameLine();

        var triggerless = _script.Triggerless;
        if (ImGui.Checkbox("Triggerless##hdr", ref triggerless))
        {
            using (Eddy.History.BeginScope("Edit Script Triggerless"))
            {
                _script.Triggerless = triggerless;
            }
        }

        ImGui.SameLine();

        var ignoreEndTriggers = _script.IgnoreEndTriggers;
        if (ImGui.Checkbox("Ignore End-Triggers##hdr", ref ignoreEndTriggers))
        {
            using (Eddy.History.BeginScope("Edit Script Ignore End-Triggers"))
            {
                _script.IgnoreEndTriggers = ignoreEndTriggers;
            }
        }

        ImGui.SameLine();

        var isWinCondition = _script.IsWinCondition;
        if (ImGui.Checkbox("Completion Condition##hdr", ref isWinCondition))
        {
            using (Eddy.History.BeginScope("Edit Script Completion Condition"))
            {
                _script.IsWinCondition = isWinCondition;
            }
        }
    }

    private static ScriptApiEntry? FindEntry(string typeName)
    {
        return Array.Find(ScriptingApi.Entries, e => e.TypeName == typeName);
    }

    private void DrawEntityFields(Entity entity, ref string dependentField, string scopeLabel)
    {
        var typeNames = Array.ConvertAll(ScriptingApi.Entries, e => e.TypeName);
        var typeIdx = Array.IndexOf(typeNames, entity.Type);
        var currentType = typeIdx >= 0 ? typeNames[typeIdx] : "";

        if (ImGui.BeginCombo("Entity Type", currentType))
        {
            for (var i = 0; i < typeNames.Length; i++)
            {
                var selected = i == typeIdx;
                if (ImGui.Selectable(typeNames[i], selected))
                {
                    using (Eddy.History.BeginScope($"Change {scopeLabel} Entity Type"))
                    {
                        entity.Type = typeNames[i];
                        dependentField = "";
                        entity.Identifier = null;
                    }
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        var entry = FindEntry(entity.Type);
        if (entry is not { IsStatic: true })
        {
            var id = entity.Identifier?.ToString(CultureInfo.InvariantCulture) ?? "";
            if (ImGui.InputText("Identifier", ref id, 11, ImGuiInputTextFlags.CharsDecimal))
            {
                int? identifier = null;
                var isValid = id.Length == 0;
                if (!isValid && int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    identifier = parsed;
                    isValid = true;
                }

                if (isValid)
                {
                    using (Eddy.History.BeginScope($"Change {scopeLabel} Entity Identifier"))
                    {
                        entity.Identifier = identifier;
                    }
                }
            }

            var isPicking = _pickTarget == entity;
            if (isPicking)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button($"{Lucide.Target} Pick##{scopeLabel}"))
            {
                _pickTarget = entity;
                Eddy.Picked = new PickingState.Waiting();
                Eddy.ShowInstanceBrowser = true;
            }

            if (isPicking)
            {
                ImGui.EndDisabled();
                ImGui.SameLine();
                ImGui.TextDisabled("Click instance in browser...");
            }
        }
    }

    private void DrawTriggers()
    {
        ImGui.Text("Triggers (WHEN)");
        ImGui.Separator();

        if (ImGui.Button($"{Lucide.Plus} Add"))
        {
            using (Eddy.History.BeginScope("Add Trigger"))
            {
                _script!.Triggers = _script.Triggers.EmptyIfNull();
                _script!.Triggers.Add(new ScriptTrigger());
                _triggerIndex = _script.Triggers.Count - 1;
            }
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(_triggerIndex == -1);
        if (ImGui.Button($"{Lucide.Copy} Clone") && _script!.Triggers != null)
        {
            using (Eddy.History.BeginScope("Clone Trigger"))
            {
                var clone = Clone(_script!.Triggers[_triggerIndex]);
                _script.Triggers.Add(clone);
                _triggerIndex = _script.Triggers.Count - 1;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button($"{Lucide.Trash2} Remove") && _script!.Triggers != null)
        {
            using (Eddy.History.BeginScope("Remove Trigger"))
            {
                _script!.Triggers.RemoveAt(_triggerIndex);
                _script!.Triggers = _script.Triggers.NullIfEmpty();
                _triggerIndex = -1;
            }
        }

        ImGui.EndDisabled();

        ImGui.Separator();

        if (ImGuiX.BeginChild("##TriggerList", new Vector2(0, -TriggerFormHeight)))
        {
            if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsAnyItemHovered())
            {
                _triggerIndex = -1;
            }

            if (_script!.Triggers is not { Count: > 0 })
            {
                const string empty = "No triggers...";
                ImGuiX.SetTextCentered(empty);
                ImGui.Text(empty);
            }

            for (var i = 0; i < _script.Triggers?.Count; i++)
            {
                var trigger = _script.Triggers[i];
                if (ImGui.Selectable(trigger.Stringify() + $"##{i}", _triggerIndex == i))
                {
                    _triggerIndex = i;
                }
            }

            ImGui.EndChild();
        }

        ImGui.Separator();

        if (ImGuiX.BeginChild("##TriggerForm", Vector2.Zero))
        {
            if (_triggerIndex >= 0 && _triggerIndex < _script!.Triggers?.Count)
            {
                var t = _script.Triggers[_triggerIndex];

                var tEvent = t.Event;
                DrawEntityFields(t.Object, ref tEvent, "Trigger");
                if (!string.Equals(tEvent, t.Event, StringComparison.Ordinal))
                {
                    t.Event = tEvent;
                }

                var triggerEntry = FindEntry(t.Object.Type);
                var eventNames = triggerEntry != null
                    ? Array.ConvertAll(triggerEntry.Triggers, tr => tr.Name)
                    : Array.Empty<string>();

                if (eventNames.Length > 0)
                {
                    var eventIdx = Array.IndexOf(eventNames, t.Event);
                    var currentEvent = eventIdx >= 0 ? eventNames[eventIdx] : "";
                    if (ImGui.BeginCombo("Event", currentEvent))
                    {
                        for (var ei = 0; ei < eventNames.Length; ei++)
                        {
                            var selected = ei == eventIdx;
                            if (ImGui.Selectable(eventNames[ei], selected))
                            {
                                using (Eddy.History.BeginScope("Change Trigger Event"))
                                {
                                    t.Event = eventNames[ei];
                                }
                            }

                            var evDesc = triggerEntry!.Triggers[ei].Description;
                            if (evDesc != null)
                            {
                                ImGui.SetItemTooltip(evDesc);
                            }

                            if (selected)
                            {
                                ImGui.SetItemDefaultFocus();
                            }
                        }

                        ImGui.EndCombo();
                    }
                }
                else
                {
                    DrawEmptyCombo("Event");
                }
            }

            ImGui.EndChild();
        }
    }

    private void DrawConditions()
    {
        ImGui.Text("Conditions (IF)");
        ImGui.Separator();

        if (ImGui.Button($"{Lucide.Plus} Add"))
        {
            using (Eddy.History.BeginScope("Add Condition"))
            {
                _script!.Conditions = _script.Conditions.EmptyIfNull();
                _script!.Conditions.Add(new ScriptCondition());
                _conditionIndex = _script.Conditions.Count - 1;
            }
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(_conditionIndex == -1);
        if (ImGui.Button($"{Lucide.Copy} Clone") && _script!.Conditions != null)
        {
            using (Eddy.History.BeginScope("Clone Condition"))
            {
                var clone = Clone(_script!.Conditions[_conditionIndex]);
                _script.Conditions.Add(clone);
                _conditionIndex = _script.Conditions.Count - 1;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button($"{Lucide.Trash2} Remove") && _script!.Conditions != null)
        {
            using (Eddy.History.BeginScope("Remove Condition"))
            {
                _script!.Conditions.RemoveAt(_conditionIndex);
                _script!.Conditions = _script.Conditions.NullIfEmpty();
                _conditionIndex = -1;
            }
        }

        ImGui.EndDisabled();

        ImGui.Separator();

        if (ImGuiX.BeginChild("##ConditionList", new Vector2(0, -ConditionFormHeight)))
        {
            if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsAnyItemHovered())
            {
                _conditionIndex = -1;
            }

            if (_script!.Conditions is not { Count: > 0 })
            {
                const string empty = "No conditions...";
                ImGuiX.SetTextCentered(empty);
                ImGui.Text(empty);
            }

            for (var i = 0; i < _script.Conditions?.Count; i++)
            {
                var cond = _script.Conditions[i];
                if (ImGui.Selectable(cond.Stringify() + $"##{i}", _conditionIndex == i))
                {
                    _conditionIndex = i;
                }
            }

            ImGui.EndChild();
        }

        ImGui.Separator();

        if (ImGuiX.BeginChild("##ConditionForm", Vector2.Zero))
        {
            if (_conditionIndex >= 0 && _conditionIndex < _script!.Conditions?.Count)
            {
                var c = _script.Conditions[_conditionIndex];

                var cProp = c.Property;
                DrawEntityFields(c.Object, ref cProp, "Condition");
                if (!string.Equals(cProp, c.Property, StringComparison.Ordinal))
                {
                    c.Property = cProp;
                }

                var condEntry = FindEntry(c.Object.Type);
                var propNames = condEntry != null
                    ? Array.ConvertAll(condEntry.Conditions, cd => cd.Name)
                    : Array.Empty<string>();

                if (propNames.Length > 0)
                {
                    var propIdx = Array.IndexOf(propNames, c.Property);
                    var currentProp = propIdx >= 0 ? propNames[propIdx] : "";
                    if (ImGui.BeginCombo("Property", currentProp))
                    {
                        for (var pi = 0; pi < propNames.Length; pi++)
                        {
                            var selected = pi == propIdx;
                            if (ImGui.Selectable(propNames[pi], selected))
                            {
                                using (Eddy.History.BeginScope("Change Condition Property"))
                                {
                                    c.Property = propNames[pi];
                                }
                            }

                            var propDesc = condEntry!.Conditions[pi].Description;
                            if (propDesc != null)
                            {
                                ImGui.SetItemTooltip(propDesc);
                            }

                            if (selected)
                            {
                                ImGui.SetItemDefaultFocus();
                            }
                        }

                        ImGui.EndCombo();
                    }
                }
                else
                {
                    DrawEmptyCombo("Property");
                }

                // Display symbols parallel to Enum.GetNames order
                if (ImGui.BeginCombo("Operator", c.Operator.Stringify()))
                {
                    var operators = Enum.GetValues<ComparisonOperator>();
                    for (var i = 0; i < operators.Length; i++)
                    {
                        var selected = i == Array.IndexOf(operators, c.Operator);
                        if (ImGui.Selectable(operators[i].Stringify(), selected))
                        {
                            using (Eddy.History.BeginScope("Change Condition Operator"))
                            {
                                c.Operator = operators[i];
                            }
                        }

                        if (selected)
                        {
                            ImGui.SetItemDefaultFocus();
                        }
                    }

                    ImGui.EndCombo();
                }

                var value = c.Value;
                if (ImGui.InputText("Value", ref value, 255))
                {
                    using (Eddy.History.BeginScope("Change Condition Value"))
                    {
                        c.Value = value;
                    }
                }
            }

            ImGui.EndChild();
        }
    }

    private void DrawActions()
    {
        ImGui.Text("Actions (WHAT)");
        ImGui.Separator();

        if (ImGui.Button($"{Lucide.Plus} Add"))
        {
            using (Eddy.History.BeginScope("Add Action"))
            {
                _script!.Actions = _script!.Actions.EmptyIfNull();
                _script!.Actions.Add(new ScriptAction());
                _actionIndex = _script.Actions.Count - 1;
            }
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(_actionIndex == -1);
        if (ImGui.Button($"{Lucide.Copy} Clone") && _script!.Actions != null)
        {
            using (Eddy.History.BeginScope("Clone Action"))
            {
                var clone = Clone(_script!.Actions[_actionIndex]);
                _script.Actions.Add(clone);
                _actionIndex = _script.Actions.Count - 1;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button($"{Lucide.Trash2} Remove") && _script!.Actions != null)
        {
            using (Eddy.History.BeginScope("Remove Action"))
            {
                _script!.Actions.RemoveAt(_actionIndex);
                _script!.Actions = _script.Actions.NullIfEmpty();
                _actionIndex = -1;
            }
        }

        ImGui.EndDisabled();

        ImGui.Separator();

        if (ImGuiX.BeginChild("##ActionList", new Vector2(0, -ActionFormHeight)))
        {
            if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsAnyItemHovered())
            {
                _actionIndex = -1;
            }

            if (_script!.Actions is not { Count: > 0 })
            {
                const string empty = "No actions...";
                ImGuiX.SetTextCentered(empty);
                ImGui.Text(empty);
            }

            for (var i = 0; i < _script.Actions?.Count; i++)
            {
                var action = _script.Actions[i];
                if (ImGui.Selectable(action.Stringify() + $"##{i}", _actionIndex == i))
                {
                    _actionIndex = i;
                }
            }

            ImGui.EndChild();
        }

        ImGui.Separator();

        if (ImGuiX.BeginChild("##ActionForm", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar))
        {
            if (_actionIndex >= 0 && _actionIndex < _script!.Actions?.Count)
            {
                var a = _script.Actions[_actionIndex];

                var aOp = a.Operation;
                DrawEntityFields(a.Object, ref aOp, "Action");
                if (!string.Equals(aOp, a.Operation, StringComparison.Ordinal))
                {
                    a.Operation = aOp;
                }

                var actionEntry = FindEntry(a.Object.Type);
                var opNames = actionEntry != null
                    ? Array.ConvertAll(actionEntry.Actions, ac => ac.Name)
                    : Array.Empty<string>();

                if (opNames.Length > 0)
                {
                    var opIdx = Array.IndexOf(opNames, a.Operation);
                    var currentOp = opIdx >= 0 ? opNames[opIdx] : "";
                    if (ImGui.BeginCombo("Operation", currentOp))
                    {
                        for (var oi = 0; oi < opNames.Length; oi++)
                        {
                            var selected = oi == opIdx;
                            if (ImGui.Selectable(opNames[oi], selected))
                            {
                                using (Eddy.History.BeginScope("Change Action Operation"))
                                {
                                    a.Operation = opNames[oi];
                                    var newActionDef = actionEntry!.Actions[oi];
                                    var expectedCount = newActionDef.Parameters.Length;
                                    var newArgs = new string[expectedCount];
                                    for (var i = 0; i < expectedCount; i++)
                                    {
                                        newArgs[i] = i < a.Arguments.Length ? a.Arguments[i] : "";
                                    }

                                    a.Arguments = newArgs;
                                }
                            }

                            var opDesc = actionEntry!.Actions[oi].Description;
                            if (opDesc != null)
                            {
                                ImGui.SetItemTooltip(opDesc);
                            }

                            if (selected)
                            {
                                ImGui.SetItemDefaultFocus();
                            }
                        }

                        ImGui.EndCombo();
                    }
                }
                else
                {
                    DrawEmptyCombo("Operation");
                }

                var killSwitch = a.Killswitch;
                if (ImGui.Checkbox("Kill-switch", ref killSwitch))
                {
                    using (Eddy.History.BeginScope("Change Action Kill-switch"))
                    {
                        a.Killswitch = killSwitch;
                    }
                }

                ImGui.SameLine();

                var blocking = a.Blocking;
                if (ImGui.Checkbox("Stop-and-Wait Before", ref blocking))
                {
                    using (Eddy.History.BeginScope("Change Action Blocking"))
                    {
                        a.Blocking = blocking;
                    }
                }

                var currentActionDef = actionEntry?.Actions
                    .FirstOrDefault(ac => ac.Name == a.Operation);

                if (currentActionDef is { Parameters.Length: > 0 })
                {
                    if (a.Arguments.Length != currentActionDef.Parameters.Length)
                    {
                        var synced = new string[currentActionDef.Parameters.Length];
                        for (var i = 0; i < synced.Length; i++)
                        {
                            synced[i] = i < a.Arguments.Length ? a.Arguments[i] : "";
                        }

                        a.Arguments = synced;
                    }

                    ImGui.SeparatorText("Arguments");

                    for (var i = 0; i < currentActionDef.Parameters.Length; i++)
                    {
                        var param = currentActionDef.Parameters[i];
                        var arg = a.Arguments[i];
                        if (ImGui.InputText($"{param.Name}##{i}", ref arg, 255))
                        {
                            using (Eddy.History.BeginScope($"Change Action Argument [{param.Name}]"))
                            {
                                a.Arguments[i] = arg;
                            }
                        }
                    }
                }
            }

            ImGui.EndChild();
        }
    }

    private static void DrawEmptyCombo(string label)
    {
        ImGui.BeginDisabled();
        if (ImGui.BeginCombo(label, ""))
        {
            ImGui.EndCombo();
        }

        ImGui.EndDisabled();
    }

    private static T Clone<T>(T obj) where T : class
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj))!;
    }
}