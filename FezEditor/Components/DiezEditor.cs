using FEZRepacker.Core.Definitions.Game.TrackedSong;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class DiezEditor : EditorComponent
{
    public override object Asset => _trackedSong;
    
    private readonly TrackedSong _trackedSong;
    
    private int _loopIndex = -1;

    public DiezEditor(Game game, string title, TrackedSong trackedSong) : base(game, title)
    {
        _trackedSong = trackedSong;
        History.Track(trackedSong);
    }

    public override void Draw()
    {
        ImGuiX.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 8));
        
        var availSize = ImGui.GetContentRegionAvail();
        var width = availSize.X / 3f;
        
        #region Song Properties
        
        if (ImGuiX.BeginChild("SongProperties", new Vector2(width, 0), ImGuiChildFlags.Border))
        {
            ImGui.Text("Tracked Song Properties");
            ImGui.Separator();
            
            var name = _trackedSong.Name;
            if (ImGui.InputText("Song Name", ref name, 255))
            {
                using (History.BeginScope("Change Name"))
                {
                    _trackedSong.Name = name;
                }
            }

            var tempo = _trackedSong.Tempo;
            if (ImGui.InputInt("Tempo", ref tempo, 10, 100))
            {
                using (History.BeginScope("Change Tempo"))
                {
                    _trackedSong.Tempo = tempo;
                }
            }

            var timeSignature = _trackedSong.TimeSignature;
            if (ImGui.InputInt("Time Signature /4", ref timeSignature, 1, 1))
            {
                using (History.BeginScope("Change Time Signature"))
                {
                    _trackedSong.TimeSignature = timeSignature;
                }
            }
            
            ImGui.SeparatorText("Notes");
            {
                var assembleChord = (int)_trackedSong.AssembleChord;
                var assembleChords = Enum.GetNames<AssembleChords>();
                if (ImGui.Combo("Assemble Chord", ref assembleChord, assembleChords, assembleChords.Length))
                {
                    using (History.BeginScope("Change Assemble Chord"))
                    {
                        _trackedSong.AssembleChord = (AssembleChords)assembleChord;
                    }
                }
                
                var notes = Enum.GetNames<ShardNotes>();
                for (var i = 0; i < 8; i++)
                {
                    var note = (int)_trackedSong.Notes[i];
                    if (ImGui.Combo($"Note #{i+1}", ref note, notes, notes.Length))
                    {
                        using (History.BeginScope("Change Shard Note"))
                        {
                            _trackedSong.Notes[i] = (ShardNotes)note;
                        }
                    }
                }
                
                if (ImGui.Button("(>) Play Assemble Chord to Preview"))
                {
                    // TODO: Play chord sound
                }
            }
            
            ImGui.SeparatorText("Ordering");
            {
                var randomOrdering = _trackedSong.RandomOrdering;
                if (ImGui.Checkbox("Random One-At-a-Time Ordering", ref randomOrdering))
                {
                    using (History.BeginScope("Change Random Ordering"))
                    {
                        _trackedSong.RandomOrdering = randomOrdering;
                    }
                }
                
                ImGui.Text("Custom Ordering:");
                if (ImGuiX.BeginChild("CustomOrdering", Vector2.Zero, ImGuiChildFlags.Border))
                {
                    for (var i = 0; i < _trackedSong.CustomOrdering.Length; i++)
                    {
                        ImGui.PushID(i);
                        {
                            ImGui.Text($"{i + 1}.");
                            ImGui.SameLine();
        
                            var value = _trackedSong.CustomOrdering[i];
                            if (ImGui.InputInt("##value", ref value, 0, 0))
                            {
                                using (History.BeginScope("Change Custom Ordering"))
                                {
                                    _trackedSong.CustomOrdering[i] = value;
                                }
                            }
        
                            ImGui.SameLine();
                            if (ImGui.Button("(-)"))
                            {
                                using (History.BeginScope("Remove Custom Ordering"))
                                {
                                    _trackedSong.CustomOrdering = _trackedSong.CustomOrdering
                                        .Where((_, index) => index != i)
                                        .ToArray();
                                    i--;
                                }
                            }
                        }
                        ImGui.PopID();
                    }

                    if (ImGui.Button("(+) Add New Value"))
                    {
                        using (History.BeginScope("Add Custom Ordering"))
                        {
                            _trackedSong.CustomOrdering = _trackedSong.CustomOrdering
                                .Append(0)
                                .ToArray();
                        }
                    }
                    
                    ImGui.EndChild();
                }
            }

            ImGui.EndChild();
        }
        
        #endregion

        ImGui.SameLine();
        
        #region Loops list

        if (ImGuiX.BeginChild("OverlayLoops", new Vector2(width, 0), ImGuiChildFlags.Border))
        {
            ImGui.Text("Overlay Loops");
            ImGui.Separator();
            
            if (ImGui.Button("(_) Down"))
            {
                using (History.BeginScope("Change Loops Order"))
                {
                    // TODO: Move loop down
                }
            }
            
            ImGui.SameLine();
            if (ImGui.Button("(^) Up"))
            {
                using (History.BeginScope("Change Loops Order"))
                {
                    // TODO: Move loop up
                }
            }
            
            ImGui.SameLine();
            if (ImGui.Button("(+) Add"))
            {
                using (History.BeginScope("Add New Loop"))
                {
                    // TODO: Add new loop
                }
            }
            
            ImGui.SameLine();
            if (ImGui.Button("(-) Remove"))
            {
                using (History.BeginScope("Remove The Loop"))
                {
                    // TODO: Remove the loop
                }
            }
            
            ImGui.Separator();
            if (ImGui.BeginChild("##LoopsList"))
            {
                if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsAnyItemHovered())
                {
                    _loopIndex = -1;
                }

                if (_trackedSong.Loops.Count == 0)
                {
                    const string emptyText = "There's no loops...";
                    ImGuiX.SetTextCentered(emptyText);
                    ImGui.Text(emptyText);
                }
                
                for (var i = 0; i < _trackedSong.Loops.Count; i++)
                {
                    var loop = _trackedSong.Loops[i];
                    if (ImGui.Selectable(loop.Name, _loopIndex == i))
                    {
                        _loopIndex = i;
                    }
                }

                ImGui.EndChild();
            }
            
            ImGui.EndChild();
        }
        
        #endregion

        ImGui.SameLine();
        
        #region Overlay loops
        
        if (ImGuiX.BeginChild("LoopProperties", Vector2.Zero, ImGuiChildFlags.Border))
        {
            const string text = "Selected Loop Properties";
            ImGui.Text(text);
            ImGui.Separator();
            
            if (_loopIndex == -1)
            {
                const string emptyText = "Select Loop from the list";
                ImGuiX.SetTextCentered(emptyText);
                ImGui.Text(emptyText);
                ImGui.EndChild();
                return;
            }
            
            var loop = _trackedSong.Loops[_loopIndex];
            
            ImGui.SeparatorText("Filename");
            {
                var filename = loop.Name;
                if (ImGui.InputText("##Filename", ref filename, 255))
                {
                    using (History.BeginScope("Change Loop Name"))
                    {
                        loop.Name = filename;
                    }
                }
            }
            
            ImGui.SeparatorText("Trigger between after every...");
            {
                var triggerFrom = loop.TriggerFrom;
                ImGui.SetNextItemWidth(96);
                if (ImGui.InputInt("##TriggerFrom", ref triggerFrom))
                {
                    using (History.BeginScope("Change Loop Trigger From"))
                    {
                        loop.TriggerFrom = triggerFrom;
                    }
                }
                
                ImGui.SameLine();
                ImGui.Text("and");
                ImGui.SameLine();
            
                var triggerTo = loop.TriggerTo;
                ImGui.SetNextItemWidth(96);
                if (ImGui.InputInt("bars...##TriggerTo", ref triggerTo))
                {
                    using (History.BeginScope("Change Loop Trigger To"))
                    {
                        loop.TriggerTo = triggerTo;
                    }
                }

                var fractional = loop.FractionalTime;
                if (ImGui.Checkbox("Fractional Time", ref fractional))
                {
                    using (History.BeginScope("Change Loop Fractional Time"))
                    {
                        loop.FractionalTime = fractional;
                    }
                }
            }
            
            ImGui.SeparatorText("...and loop between...");
            {
                var loopTimesFrom = loop.LoopTimesFrom;
                ImGui.SetNextItemWidth(96);
                if (ImGui.InputInt("##TimesFrom", ref loopTimesFrom))
                {
                    using (History.BeginScope("Change Loop Times From"))
                    {
                        loop.LoopTimesFrom = loopTimesFrom;
                    }
                }
                
                ImGui.SameLine();
                ImGui.Text("and");
                ImGui.SameLine();
            
                var loopTimesTo = loop.LoopTimesTo;
                ImGui.SetNextItemWidth(96);
                if (ImGui.InputInt("times.##TimesTo", ref loopTimesTo))
                {
                    using (History.BeginScope("Change Loop Times To"))
                    {
                        loop.LoopTimesTo = loopTimesTo;
                    }
                }
            }
            

            ImGui.SeparatorText("The loop is...");
            {
                var duration = loop.Duration;
                if (ImGui.InputInt("bars long.##Duration", ref duration))
                {
                    using (History.BeginScope("Change Loop Duration"))
                    {
                        loop.Duration = duration;
                    }
                }
            }
            
            ImGui.SeparatorText("Delay first trigger by...");
            {
                var delay = loop.Delay;
                if (ImGui.InputInt("bars.##Delay", ref delay))
                {
                    using (History.BeginScope("Change Loop Delay"))
                    {
                        loop.Delay = delay;
                    }
                }
            }
            
            var oneAtATime = loop.OneAtATime;
            if (ImGui.Checkbox("One-at-a-time", ref oneAtATime))
            {
                using (History.BeginScope("Change Loop One At a Time"))
                {
                    loop.OneAtATime = oneAtATime;
                }
            }
            
            ImGui.SameLine();
            
            var cutOffTail = loop.CutOffTail;
            if (ImGui.Checkbox("Cut off tail", ref cutOffTail))
            {
                using (History.BeginScope("Change Loop Cut Off Tail"))
                {
                    loop.CutOffTail = cutOffTail;
                }
            }
            
            ImGui.SeparatorText("Time of day");
            {
                var day = loop.Day;
                if (ImGui.Checkbox("Day", ref day))
                {
                    using (History.BeginScope("Change Loop Day"))
                    {
                        loop.Day = day;
                    }
                }
                
                ImGui.SameLine();
                
                var night = loop.Night;
                if (ImGui.Checkbox("Night", ref night))
                {
                    using (History.BeginScope("Change Loop Night"))
                    {
                        loop.Night = night;
                    }
                }
                
                ImGui.SameLine();
                
                var dawn = loop.Dawn;
                if (ImGui.Checkbox("Dawn", ref dawn))
                {
                    using (History.BeginScope("Change Loop Dawn"))
                    {
                        loop.Dawn = dawn;
                    }
                }
                
                ImGui.SameLine();
                
                var dusk = loop.Dusk;
                if (ImGui.Checkbox("Dusk", ref dusk))
                {
                    using (History.BeginScope("Change Loop Dusk"))
                    {
                        loop.Dusk = dusk;
                    }
                }
            }
            
            ImGui.EndChild();
        }

        #endregion
        
        ImGui.PopStyleVar();
    }
}