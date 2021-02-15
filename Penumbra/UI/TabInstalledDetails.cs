using Penumbra.Models;
using ImGuiNET;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

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
        private class PluginDetails
        {
            #region ========== Literals ===============
            private const string LabelPluginDetails      = "PenumbraPluginDetails";
            private const string LabelAboutTab           = "About";
            private const string TooltipAboutEdit        = "Use Ctrl+Enter for newlines.";
            private const string LabelDescEdit           = "##descedit";
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
            private const string ButtonAddToGroup        = "Add to Group";
            private const string ButtonRemoveFromGroup   = "Remove from Group";
            private const string LabelGroupSelect        = "##groupSelect";
            private const string LabelOptionSelect       = "##optionSelect";
            private const string TextNoOptionAvailable   = "[No Option Available]";
            private const string LabelConfigurationTab   = "Configuration";
            private const string LabelNewSingleGroup     = "New Single Group";
            private const string LabelNewSingleGroupEdit = "##newSingleGroup";
            private const string LabelNewMultiGroup      = "New Multi Group";
            private const string TextDefaultGamePath     = "default";
            private const string LabelGamePathsEdit      = "Game Paths";
            private const string LabelGamePathsEditBox   = "##gamePathsEdit";
            private const string TooltipGamePathText     = "Click to copy to clipboard.";
            private static readonly string TooltipGamePathsEdit = $"Enter all game paths to add or remove, separated by '{GamePathsSeparator}'.\nUse '{TextDefaultGamePath}' to add the original file path.";
            private static readonly string TooltipFilesTabEdit  = $"{TooltipFilesTab}\nRed Files are replaced in another group or a different option in this group, but not contained in the current option.";

            private const char  GamePathsSeparator      = ';';
            private const float TextSizePadding         = 5f;
            private const float OptionSelectionWidth    = 140f;
            private const float CheckMarkSize           = 50f;
            private const float MultiEditBoxWidth       = 300f;
            private const uint  ColorGreen              = 0xFF00C800;
            private const uint  ColorYellow             = 0xFF00C8C8;
            private const uint  ColorRed                = 0xFF0000C8;
            #endregion

            #region ========== State ==================
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

            #endregion

            #region ========== Tabs ===================
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
            #endregion

            #region ========== FileList ===============
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

            private void DrawEditGroupSelector()
            {
                ImGui.SetNextItemWidth( OptionSelectionWidth );
                if (Meta.Groups.Count == 0)
                {
                    ImGui.Combo( LabelGroupSelect, ref _selectedGroupIndex, TextNoOptionAvailable, 1);
                }
                else
                {
                    if (ImGui.Combo( LabelGroupSelect, ref _selectedGroupIndex, Meta.Groups.Values.Select( G => G.GroupName ).ToArray(), Meta.Groups.Count))
                    {
                        SelectGroup();
                        SelectOption(0);
                    }
                }
            }

            private void DrawEditOptionSelector()
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth( OptionSelectionWidth );
                if (_selectedGroup?.Options.Count == 0)
                {
                    ImGui.Combo( LabelOptionSelect, ref _selectedOptionIndex, TextNoOptionAvailable, 1);
                    return;
                }

                var group = (InstallerInfo) _selectedGroup;
                if (ImGui.Combo( LabelOptionSelect, ref _selectedOptionIndex, group.Options.Select(O => O.OptionName).ToArray(), group.Options.Count))
                    SelectOption();
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

                DrawEditGroupSelector();
                ImGui.SameLine();
                DrawEditOptionSelector();
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
                        ImGui.Text(gamePath);
                        if (ImGui.IsItemClicked())
                            ImGui.SetClipboardText(gamePath);
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip( TooltipGamePathText );
                    }
                    ImGui.Unindent(indent);
                }
                else
                    Selectable(ColorYellow, ColorRed);
            }

            private void DrawFileListTabEdit()
            {
                if( ImGui.BeginTabItem( LabelFileListTab ) )
                {
                    UpdateFilenameList();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip( _editMode ? TooltipFilesTabEdit : TooltipFilesTab );

                    ImGui.SetNextItemWidth( -1 );
                    if( ImGui.ListBoxHeader( LabelFileListHeader, AutoFillSize - new Vector2(0, 1.5f * ImGui.GetTextLineHeight()) ) )
                        for(var i = 0; i < Mod.Mod.ModFiles.Count; ++i)
                            DrawFileAndGamePaths(i);

                    ImGui.ListBoxFooter();
                    
                    DrawGroupRow();
                    ImGui.EndTabItem();
                }
                else
                    _fullFilenameList = null;
            }
            #endregion

            #region ========== Configuration ==========
            #region ========== MultiSelectorEdit ==========
            private bool DrawMultiSelectorEditBegin(InstallerInfo group)
            {
                var groupName = group.GroupName;
                if (ImGuiCustom.BeginFramedGroupEdit(ref groupName)
                    && groupName != group.GroupName && !Meta.Groups.ContainsKey(groupName))
                {
                    var oldConf = Mod.Conf[group.GroupName];
                    Meta.Groups.Remove(group.GroupName);
                    Mod.Conf.Remove(group.GroupName);
                    if (groupName.Length > 0)
                    {
                        Meta.Groups[groupName] = new(){ GroupName = groupName, SelectionType = SelectType.Multi, Options = group.Options };
                        Mod.Conf[groupName] = oldConf;
                    }                    
                    return true;
                }
                return false;
            }
            private void DrawMultiSelectorEditAdd(InstallerInfo group, float nameBoxStart)
            {
                var newOption = "";
                ImGui.SetCursorPosX(nameBoxStart);
                ImGui.SetNextItemWidth(MultiEditBoxWidth);
                if (ImGui.InputText($"##new_{group.GroupName}_l", ref newOption, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (newOption.Length != 0)
                    {
                        group.Options.Add(new(){ OptionName = newOption, OptionDesc = "", OptionFiles = new() });
                        _selector.SaveCurrentMod();
                    }
                }
            }

            private void DrawMultiSelectorEdit(InstallerInfo group)
            {
                var nameBoxStart = CheckMarkSize;
                var flag         = Mod.Conf[group.GroupName];

                var modChanged   = DrawMultiSelectorEditBegin(group);
                
                for (var i = 0; i < group.Options.Count; ++i)
                {
                    var opt = group.Options[i];
                    var label = $"##{opt.OptionName}_{group.GroupName}";
                    DrawMultiSelectorCheckBox(group, i, flag, label);
                
                    ImGui.SameLine();
                    var newName = opt.OptionName;

                    if (nameBoxStart == CheckMarkSize)
                        nameBoxStart = ImGui.GetCursorPosX();

                    ImGui.SetNextItemWidth(MultiEditBoxWidth);
                    if (ImGui.InputText($"{label}_l", ref newName, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        if (newName.Length == 0)
                        {
                            group.Options.RemoveAt(i);
                            var bitmaskFront = (1 << i) - 1;
                            Mod.Conf[group.GroupName] = (flag & bitmaskFront) | ((flag & ~bitmaskFront) >> 1);
                            modChanged = true;
                        }
                        else if (newName != opt.OptionName)
                        {
                            group.Options[i] = new(){ OptionName = newName, OptionDesc = opt.OptionDesc, OptionFiles = opt.OptionFiles };
                            _selector.SaveCurrentMod();
                        }
                    }
                }

                DrawMultiSelectorEditAdd(group, nameBoxStart);

                if (modChanged)
                {
                    _selector.SaveCurrentMod();
                    Save();
                }
           
                ImGuiCustom.EndFramedGroup();
            }
            #endregion

            #region ========== SingleSelectorEdit ==========
            private bool DrawSingleSelectorEditGroup(InstallerInfo group)
            {
                var groupName = group.GroupName;
                if (ImGui.InputText($"##{groupName}_add", ref groupName, 64, ImGuiInputTextFlags.EnterReturnsTrue)
                    && !Meta.Groups.ContainsKey(groupName))
                {
                    var oldConf = Mod.Conf[group.GroupName];
                    if (groupName != group.GroupName)
                    {
                        Meta.Groups.Remove(group.GroupName);
                        Mod.Conf.Remove(group.GroupName);
                    }
                    if (groupName.Length > 0)
                    {
                        Meta.Groups.Add(groupName, new InstallerInfo(){ GroupName = groupName, Options = group.Options, SelectionType = SelectType.Single } );
                        Mod.Conf[groupName] = oldConf;
                    }
                    return true;
                }
                return false;
            }

            private float DrawSingleSelectorEdit(InstallerInfo group)
            {
                var code             = Mod.Conf[group.GroupName];
                var selectionChanged = false;
                var modChanged       = false;
                var newName          = "";
                if (ImGuiCustom.RenameableCombo($"##{group.GroupName}", ref code, ref newName, group.Options.Select( x => x.OptionName ).ToArray(), group.Options.Count))
                {
                    if (code == group.Options.Count)
                    {
                        if (newName.Length > 0)
                        {
                            selectionChanged          = true;
                            modChanged                = true;
                            Mod.Conf[group.GroupName] = code;
                            group.Options.Add(new(){ OptionName = newName, OptionDesc = "", OptionFiles = new()});
                        }
                    }
                    else
                    {
                        if (newName.Length == 0)
                        {
                            modChanged = true;
                            group.Options.RemoveAt(code);
                            if (code >= group.Options.Count)
                                code = 0;
                        }
                        else if (newName != group.Options[code].OptionName)
                        {
                            modChanged = true;
                            group.Options[code] = new Option(){ OptionName = newName, OptionDesc = group.Options[code].OptionDesc, OptionFiles = group.Options[code].OptionFiles};
                        }
                        if (Mod.Conf[group.GroupName] != code)
                        {
                            selectionChanged          = true;
                            Mod.Conf[group.GroupName] = code;
                        }
                    }
                }

                ImGui.SameLine();
                var labelEditPos = ImGui.GetCursorPosX();
                modChanged |= DrawSingleSelectorEditGroup(group);

                if (modChanged)
                    _selector.SaveCurrentMod();

                if (selectionChanged)
                    Save();

                return labelEditPos;
            }
            #endregion
            private void AddNewGroup(string newGroup, SelectType selectType)
            {
                if (!Meta.Groups.ContainsKey(newGroup) && newGroup.Length > 0)
                {
                    Meta.Groups[newGroup] = new ()
                    { 
                        GroupName = newGroup, 
                        SelectionType = selectType, 
                        Options = new()
                    } ;

                    Mod.Conf[newGroup] = 0;
                    _selector.SaveCurrentMod();
                    Save();
                }
            }

            private void DrawAddSingleGroupField(float labelEditPos)
            {
                var newGroup = "";
                if(labelEditPos == CheckMarkSize)
                {
                    ImGui.SetCursorPosX(CheckMarkSize);
                    ImGui.SetNextItemWidth(MultiEditBoxWidth);
                    if (ImGui.InputText(LabelNewSingleGroup, ref newGroup, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                        AddNewGroup(newGroup, SelectType.Single);
                }
                else
                {
                    ImGuiCustom.RightJustifiedLabel(labelEditPos, LabelNewSingleGroup );
                    if (ImGui.InputText(LabelNewSingleGroupEdit, ref newGroup, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                        AddNewGroup(newGroup, SelectType.Single);
                }
            }

            private void DrawAddMultiGroupField()
            {
                var newGroup = "";
                ImGui.SetCursorPosX(CheckMarkSize);
                ImGui.SetNextItemWidth(MultiEditBoxWidth);
                if (ImGui.InputText(LabelNewMultiGroup, ref newGroup, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                    AddNewGroup(newGroup, SelectType.Multi);
            }

            private void DrawGroupSelectorsEdit()
            {
                var labelEditPos = CheckMarkSize;
                foreach( var g in Meta.Groups.Values.Where( g => g.SelectionType == SelectType.Single ) )
                    labelEditPos = DrawSingleSelectorEdit(g);
                DrawAddSingleGroupField(labelEditPos);

                foreach(var g in Meta.Groups.Values.Where( g => g.SelectionType == SelectType.Multi ))
                    DrawMultiSelectorEdit(g);
                DrawAddMultiGroupField();
            }

            #region Non-Edit

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
            #endregion


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
            #endregion

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