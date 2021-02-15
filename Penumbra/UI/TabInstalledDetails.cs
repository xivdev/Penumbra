using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using Penumbra.Models;

namespace Penumbra.UI
{
    internal static class Extension
    {
        // Remove the entry at idx from the list if the new string is empty, otherwise replace it.
        public static void RemoveOrChange(this List<string> list, string newString, int idx)
        {
            if (newString?.Length == 0)
                list.RemoveAt(idx);
            else
                list[idx] = newString;
        }
    }

    public partial class SettingsInterface
    {
        private partial class PluginDetails
        {
            private const string LabelPluginDetails      = "PenumbraPluginDetails";
            private const string LabelAboutTab           = "About";
            private const string LabelChangedItemsTab    = "Changed Items";
            private const string LabelChangedItemsHeader = "##changedItems";
            private const string LabelChangedItemIdx     = "##citem_";
            private const string LabelChangedItemNew     = "##citem_new";
            private const string LabelConflictsTab       = "File Conflicts";
            private const string LabelConflictsHeader    = "##conflicts";
            private const string LabelFileSwapTab        = "File Swaps";
            private const string LabelFileSwapHeader     = "##fileSwaps";
            private const string LabelFileListTab        = "Files";
            private const string LabelFileListHeader     = "##fileList";
            private const string TooltipFilesTab         = "Green files replace their standard game path counterpart (not in any option) or are in all options of a Single-Select option.\nYellow files are restricted to some options.";
            private const string LabelGroupSelect        = "##groupSelect";
            private const string LabelOptionSelect       = "##optionSelect";
            private const string LabelConfigurationTab   = "Configuration";

            private const float TextSizePadding         = 5f;
            private const float OptionSelectionWidth    = 140f;
            private const float CheckMarkSize           = 50f;
            private const uint  ColorGreen              = 0xFF00C800;
            private const uint  ColorYellow             = 0xFF00C8C8;
            private const uint  ColorRed                = 0xFF0000C8;

            private bool                          _editMode             = false;
            private int                           _selectedGroupIndex   = 0;
            private InstallerInfo?                _selectedGroup        = null;
            private int                           _selectedOptionIndex  = 0;
            private Option?                       _selectedOption       = null;
            private (string label, string name)[] _changedItemsList     = null;
            private float?                        _fileSwapOffset       = null;
            private string                        _currentGamePaths     = "";

            private (string name, bool selected, uint color, string relName)[] _fullFilenameList = null;

            public void SelectGroup(int idx)
            {
                _selectedGroupIndex = idx;
                if (_selectedGroupIndex >= Meta?.Groups?.Count)
                    _selectedGroupIndex = 0;
                if (Meta?.Groups?.Count > 0)
                    _selectedGroup = Meta.Groups.ElementAt(_selectedGroupIndex).Value;
                else
                    _selectedGroup = null;
            }
            public void SelectGroup() => SelectGroup(_selectedGroupIndex);

            public void SelectOption(int idx)
            {
                _selectedOptionIndex = idx;
                if (_selectedOptionIndex >= _selectedGroup?.Options.Count)
                    _selectedOptionIndex = 0;
                if (_selectedGroup?.Options.Count > 0)
                    _selectedOption = ((InstallerInfo) _selectedGroup).Options[_selectedOptionIndex];
                else
                    _selectedOption = null;
            }
            public void SelectOption() => SelectOption(_selectedOptionIndex);

            public void ResetState()
            {
                _changedItemsList      = null;
                _fileSwapOffset        = null;
                _fullFilenameList      = null;
                SelectGroup();
                SelectOption();
            }


            private readonly Selector          _selector;
            private readonly SettingsInterface _base;
            public PluginDetails(SettingsInterface ui, Selector s)
            {
                _base     = ui;
                _selector = s;
                ResetState();
            }

            private ModInfo Mod  { get{ return _selector.Mod(); } }
            private ModMeta Meta { get{ return Mod?.Mod?.Meta; } }

            private void Save()
            {
                _base._plugin.ModManager.Mods.Save();
                _base._plugin.ModManager.CalculateEffectiveFileList();
                _base._menu._effectiveTab.RebuildFileList(_base._plugin.Configuration.ShowAdvanced);
            }

            private void DrawAboutTab()
            {
                if (!_editMode && Meta.Description?.Length == 0)
                    return;

                if(ImGui.BeginTabItem( LabelAboutTab ) )
                {
                    var desc = Meta.Description;
                    var flags = _editMode
                        ? ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.CtrlEnterForNewLine
                        : ImGuiInputTextFlags.ReadOnly;

                    if( _editMode )
                    {
                        if (ImGui.InputTextMultiline(LabelDescEdit,  ref desc, 1 << 16, AutoFillSize, flags))
                        {
                            Meta.Description = desc;
                            _selector.SaveCurrentMod();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip( TooltipAboutEdit );
                    }
                    else
                    {
                        ImGui.TextWrapped( desc );
                    }

                    ImGui.EndTabItem();
                }
            }

            private void DrawChangedItemsTab()
            {
                if (!_editMode && Meta.ChangedItems?.Count == 0)
                    return;

                var flags = _editMode
                    ? ImGuiInputTextFlags.EnterReturnsTrue
                    : ImGuiInputTextFlags.ReadOnly;

                if( ImGui.BeginTabItem( LabelChangedItemsTab ) )
                {
                    ImGui.SetNextItemWidth( -1 );
                    if( ImGui.ListBoxHeader( LabelChangedItemsHeader, AutoFillSize ) )
                    {
                        if (_changedItemsList == null)
                            _changedItemsList = Meta.ChangedItems.Select( (I, index) => ($"{LabelChangedItemIdx}{index}", I) ).ToArray();
                        for (var i = 0; i < Meta.ChangedItems.Count; ++i)
                        {
                            ImGui.SetNextItemWidth(-1);
                            if ( ImGui.InputText(_changedItemsList[i].label, ref _changedItemsList[i].name, 128, flags) )
                            {
                                Meta.ChangedItems.RemoveOrChange(_changedItemsList[i].name, i);
                                _selector.SaveCurrentMod();
                            }
                        }
                        var newItem = "";
                        if ( _editMode )
                        {
                            ImGui.SetNextItemWidth(-1);
                            if ( ImGui.InputText( LabelChangedItemNew, ref newItem, 128, flags) )
                            {
                                if (newItem.Length > 0)
                                {
                                    if (Meta.ChangedItems == null)
                                        Meta.ChangedItems = new(){ newItem };
                                    else
                                        Meta.ChangedItems.Add(newItem);
                                    _selector.SaveCurrentMod();
                                }
                            }
                        }
                        ImGui.ListBoxFooter();
                    }
                    ImGui.EndTabItem();
                }
                else
                    _changedItemsList = null;
            }

            private void DrawConflictTab()
            {
                if( Mod.Mod.FileConflicts.Any() )
                {
                    if( ImGui.BeginTabItem( LabelConflictsTab ) )
                    {
                        ImGui.SetNextItemWidth( -1 );
                        if( ImGui.ListBoxHeader( LabelConflictsHeader, AutoFillSize ) )
                        {
                            foreach( var kv in Mod.Mod.FileConflicts )
                            {
                                var mod = kv.Key;
                                if( ImGui.Selectable( mod ) )
                                    _selector.SelectModByName( mod );

                                ImGui.Indent( 15 );
                                foreach( var file in kv.Value )
                                    ImGui.Selectable( file );
                                ImGui.Unindent( 15 );
                            }
                            ImGui.ListBoxFooter();
                        }

                        ImGui.EndTabItem();
                    }
                }
            }

            private void DrawFileSwapTab()
            {
                if( Meta.FileSwaps.Any() )
                {
                    if( ImGui.BeginTabItem( LabelFileSwapTab ) )
                    {
                        if (_fileSwapOffset == null)
                            _fileSwapOffset = Meta.FileSwaps.Max( P => ImGui.CalcTextSize(P.Key).X) + TextSizePadding;
                        ImGui.SetNextItemWidth( -1 );
                        if( ImGui.ListBoxHeader( LabelFileSwapHeader, AutoFillSize ) )
                        {
                            foreach( var file in Meta.FileSwaps )
                            {
                                ImGui.Selectable(file.Key);
                                ImGui.SameLine(_fileSwapOffset ?? 0);
                                ImGui.TextUnformatted("  -> ");
                                ImGui.SameLine();
                                ImGui.Selectable(file.Value);
                            }
                            ImGui.ListBoxFooter();
                        }
                        ImGui.EndTabItem();
                    }
                    else
                        _fileSwapOffset = null;
                }
            }

            private void UpdateFilenameList()
            {
                if (_fullFilenameList == null)
                {
                    var len = Mod.Mod.ModBasePath.FullName.Length;
                    _fullFilenameList = Mod.Mod.ModFiles.Select( F => (F.FullName, false, ColorGreen, "") ).ToArray();

                    if(Meta.Groups?.Count == 0)
                        return;

                    for (var i = 0; i < Mod.Mod.ModFiles.Count; ++i)
                    {
                        _fullFilenameList[i].relName = _fullFilenameList[i].name.Substring(len).TrimStart('\\');
                        foreach (var Group in Meta.Groups.Values)
                        {
                            var inAll = true;
                            foreach (var Option in Group.Options)
                            {
                                if (Option.OptionFiles.ContainsKey(_fullFilenameList[i].relName))
                                    _fullFilenameList[i].color = ColorYellow;
                                else
                                    inAll = false;
                            }
                            if (inAll && Group.SelectionType == SelectType.Single)
                                _fullFilenameList[i].color = ColorGreen;
                        }
                    }
                }
            }

            private void DrawFileListTab()
            {
                if( ImGui.BeginTabItem( LabelFileListTab ) )
                {
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip( TooltipFilesTab );

                    ImGui.SetNextItemWidth( -1 );
                    if( ImGui.ListBoxHeader( LabelFileListHeader, AutoFillSize ) )
                    {
                        UpdateFilenameList();
                        foreach(var file in _fullFilenameList)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, file.color);
                            ImGui.Selectable(file.name);
                            ImGui.PopStyleColor();
                        }
                        ImGui.ListBoxFooter();
                    }
                    else
                        _fullFilenameList = null;
                    ImGui.EndTabItem();
                }
            }

            private void HandleSelectedFilesButton(bool remove)
            {
                if (_selectedOption == null)
                    return;
                var option = (Option) _selectedOption;

                var gamePaths = _currentGamePaths.Split(';');
                if (gamePaths.Length == 0 || gamePaths[0].Length == 0)
                    return;

                int? defaultIndex = null;
                for (var i = 0; i < gamePaths.Length; ++i)
                {
                    if (gamePaths[i] == TextDefaultGamePath )
                    {
                        defaultIndex = i;
                        break;
                    }
                }

                var baseLength = Mod.Mod.ModBasePath.FullName.Length;
                var changed    = false;
                for (var i = 0; i < Mod.Mod.ModFiles.Count; ++i)
                {
                    if (!_fullFilenameList[i].selected)
                        continue;

                    var fileName = _fullFilenameList[i].relName;
                    if (defaultIndex != null)
                        gamePaths[(int)defaultIndex] = fileName.Replace('\\', '/');

                    if (remove && option.OptionFiles.TryGetValue(fileName, out var setPaths))
                    {
                        if (setPaths.RemoveWhere( P => gamePaths.Contains(P)) > 0)
                            changed = true;
                        if (setPaths.Count == 0 && option.OptionFiles.Remove(fileName))
                            changed = true;
                    }
                    else
                    {
                        foreach(var gamePath in gamePaths)
                            changed |= option.AddFile(fileName, gamePath);
                    }
                }
                if (changed)
                    _selector.SaveCurrentMod();
            }

            private void DrawAddToGroupButton()
            {
                if (ImGui.Button( ButtonAddToGroup ) )
                    HandleSelectedFilesButton(false);
            }

            private void DrawRemoveFromGroupButton()
            {
                if (ImGui.Button( ButtonRemoveFromGroup ) )
                    HandleSelectedFilesButton(true);
            }

            private void DrawGamePathInput()
            {
                ImGui.TextUnformatted( LabelGamePathsEdit );
                ImGui.SameLine();
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText(LabelGamePathsEditBox, ref _currentGamePaths, 128);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(TooltipGamePathsEdit);
            }

            private void DrawGroupRow()
            {
                if (_selectedGroup == null)
                    SelectGroup();
                if (_selectedOption == null)
                    SelectOption();

                if (!DrawEditGroupSelector())
                    return;
                ImGui.SameLine();
                if (!DrawEditOptionSelector())
                    return;
                ImGui.SameLine();
                DrawAddToGroupButton();
                ImGui.SameLine();
                DrawRemoveFromGroupButton();
                ImGui.SameLine();
                DrawGamePathInput();
            }

            private void DrawFileAndGamePaths(int idx)
            {
                void Selectable(uint colorNormal, uint colorReplace)
                {
                    var loc = _fullFilenameList[idx].color;
                    if (loc == colorNormal)
                        loc = colorReplace;
                    ImGui.PushStyleColor(ImGuiCol.Text, loc);
                    ImGui.Selectable( _fullFilenameList[idx].name, ref _fullFilenameList[idx].selected );
                    ImGui.PopStyleColor();
                }

                const float indent = 30f;
                if (_selectedOption == null)
                {
                    Selectable(0, ColorGreen);
                    return;
                }

                var fileName = _fullFilenameList[idx].relName;
                if (((Option) _selectedOption).OptionFiles.TryGetValue(fileName, out var gamePaths))
                {
                    Selectable(0, ColorGreen);

                    ImGui.Indent(indent);
                    foreach (var gamePath in gamePaths)
                    {
                        string tmp = gamePath;
                        if (ImGui.InputText($"##{fileName}_{gamePath}", ref tmp, 128, ImGuiInputTextFlags.EnterReturnsTrue))
                        {
                            if (tmp != gamePath)
                            {
                                gamePaths.Remove(gamePath);
                                if (tmp.Length > 0)
                                    gamePaths.Add(tmp);
                                _selector.SaveCurrentMod();
                                _selector.ReloadCurrentMod();
                            }
                        }
                    }
                    ImGui.Unindent(indent);
                }
                else
                    Selectable(ColorYellow, ColorRed);
            }

            private void DrawMultiSelectorCheckBox(InstallerInfo group, int idx, int flag, string label)
            {
                var opt = group.Options[idx];
                var enabled = ( flag & (1 << idx)) != 0;
                var oldEnabled = enabled;
                if (ImGui.Checkbox(label, ref enabled))
                {
                    if (oldEnabled != enabled)
                    {
                        Mod.Conf[group.GroupName] ^= (1 << idx);
                        Save();
                    }
                }
            }

            private void DrawMultiSelector(InstallerInfo group)
            {
                if (group.Options.Count == 0)
                    return;

                ImGuiCustom.BeginFramedGroup(group.GroupName);
                for(var i = 0; i < group.Options.Count; ++i)
                    DrawMultiSelectorCheckBox(group, i, Mod.Conf[group.GroupName], $"{group.Options[i].OptionName}##{group.GroupName}");

                ImGuiCustom.EndFramedGroup();
            }

            private void DrawSingleSelector(InstallerInfo group)
            {
                if (group.Options.Count < 2)
                    return;
                var code = Mod.Conf[group.GroupName];
                if( ImGui.Combo( group.GroupName, ref code, group.Options.Select( x => x.OptionName ).ToArray(), group.Options.Count ) )
                {
                    Mod.Conf[group.GroupName] = code;
                    Save();
                }
            }

            private void DrawGroupSelectors()
            {
                foreach(var g in Meta.Groups.Values.Where( g => g.SelectionType == SelectType.Single ) )
                    DrawSingleSelector(g);
                foreach(var g in Meta.Groups.Values.Where( g => g.SelectionType == SelectType.Multi ))
                    DrawMultiSelector(g);
                return;
            }

            private void DrawConfigurationTab()
            {
                if (!_editMode && !Meta.HasGroupWithConfig)
                    return;

                if(ImGui.BeginTabItem( LabelConfigurationTab ) )
                {
                    if (_editMode)
                        DrawGroupSelectorsEdit();
                    else
                        DrawGroupSelectors();
                    ImGui.EndTabItem();
                }
            }

            public void Draw(bool editMode)
            {
                _editMode = editMode;
                ImGui.BeginTabBar( LabelPluginDetails );

                DrawAboutTab();
                DrawChangedItemsTab();
                DrawConfigurationTab();
                if (_editMode)
                    DrawFileListTabEdit();
                else
                    DrawFileListTab();
                DrawFileSwapTab();
                DrawConflictTab();

                ImGui.EndTabBar();
            }
        }
    }
}
