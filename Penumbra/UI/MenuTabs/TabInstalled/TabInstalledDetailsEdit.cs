using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using Penumbra.GameData.Util;
using Penumbra.Mods;
using Penumbra.Structs;
using Penumbra.UI.Custom;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private partial class PluginDetails
        {
            private const string LabelDescEdit           = "##descedit";
            private const string LabelNewSingleGroupEdit = "##newSingleGroup";
            private const string LabelNewMultiGroup      = "##newMultiGroup";
            private const string LabelGamePathsEditBox   = "##gamePathsEdit";
            private const string ButtonAddToGroup        = "Add to Group";
            private const string ButtonRemoveFromGroup   = "Remove from Group";
            private const string TooltipAboutEdit        = "Use Ctrl+Enter for newlines.";
            private const string TextNoOptionAvailable   = "[Not Available]";
            private const string TextDefaultGamePath     = "default";
            private const char   GamePathsSeparator      = ';';

            private static readonly string TooltipFilesTabEdit =
                $"{TooltipFilesTab}\n"
              + $"Red Files are replaced in another group or a different option in this group, but not contained in the current option.";

            private static readonly string TooltipGamePathsEdit =
                $"Enter all game paths to add or remove, separated by '{GamePathsSeparator}'.\n"
              + $"Use '{TextDefaultGamePath}' to add the original file path."
              + $"Use '{TextDefaultGamePath}-#' to skip the first # relative directories.";

            private const float MultiEditBoxWidth = 300f;

            private bool DrawEditGroupSelector()
            {
                ImGui.SetNextItemWidth( OptionSelectionWidth * ImGuiHelpers.GlobalScale );
                if( Meta!.Groups.Count == 0 )
                {
                    ImGui.Combo( LabelGroupSelect, ref _selectedGroupIndex, TextNoOptionAvailable, 1 );
                    return false;
                }

                if( ImGui.Combo( LabelGroupSelect, ref _selectedGroupIndex
                    , Meta.Groups.Values.Select( g => g.GroupName ).ToArray()
                    , Meta.Groups.Count ) )
                {
                    SelectGroup();
                    SelectOption( 0 );
                }

                return true;
            }

            private bool DrawEditOptionSelector()
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth( OptionSelectionWidth );
                if( ( _selectedGroup?.Options.Count ?? 0 ) == 0 )
                {
                    ImGui.Combo( LabelOptionSelect, ref _selectedOptionIndex, TextNoOptionAvailable, 1 );
                    return false;
                }

                var group = ( OptionGroup )_selectedGroup!;
                if( ImGui.Combo( LabelOptionSelect, ref _selectedOptionIndex, group.Options.Select( o => o.OptionName ).ToArray(),
                    group.Options.Count ) )
                {
                    SelectOption();
                }

                return true;
            }

            private void DrawFileListTabEdit()
            {
                if( ImGui.BeginTabItem( LabelFileListTab ) )
                {
                    UpdateFilenameList();
                    if( ImGui.IsItemHovered() )
                    {
                        ImGui.SetTooltip( _editMode ? TooltipFilesTabEdit : TooltipFilesTab );
                    }

                    ImGui.SetNextItemWidth( -1 );
                    if( ImGui.BeginListBox( LabelFileListHeader, AutoFillSize - Vector2.UnitY * 1.5f * ImGui.GetTextLineHeight() ) )
                    {
                        for( var i = 0; i < Mod!.Data.Resources.ModFiles.Count; ++i )
                        {
                            DrawFileAndGamePaths( i );
                        }
                    }

                    ImGui.EndListBox();

                    DrawGroupRow();
                    ImGui.EndTabItem();
                }
                else
                {
                    _fullFilenameList = null;
                }
            }

            private ImGuiRaii.EndStack DrawMultiSelectorEditBegin( OptionGroup group )
            {
                var groupName = group.GroupName;
                if( ImGuiCustom.BeginFramedGroupEdit( ref groupName ) )
                {
                    if( _modManager.ChangeModGroup( group.GroupName, groupName, Mod.Data ) && Mod.Data.Meta.RefreshHasGroupsWithConfig() )
                    {
                        _selector.Cache.TriggerFilterReset();
                    }
                }

                return ImGuiRaii.DeferredEnd( ImGuiCustom.EndFramedGroup );
            }

            private void DrawMultiSelectorEditAdd( OptionGroup group, float nameBoxStart )
            {
                var newOption = "";
                ImGui.SetCursorPosX( nameBoxStart );
                ImGui.SetNextItemWidth( MultiEditBoxWidth * ImGuiHelpers.GlobalScale );
                if( ImGui.InputTextWithHint( $"##new_{group.GroupName}_l", "Add new option...", ref newOption, 64,
                        ImGuiInputTextFlags.EnterReturnsTrue )
                 && newOption.Length != 0 )
                {
                    group.Options.Add( new Option()
                        { OptionName = newOption, OptionDesc = "", OptionFiles = new Dictionary< RelPath, HashSet< GamePath > >() } );
                    _selector.SaveCurrentMod();
                    if( Mod!.Data.Meta.RefreshHasGroupsWithConfig() )
                    {
                        _selector.Cache.TriggerFilterReset();
                    }
                }
            }

            private void DrawMultiSelectorEdit( OptionGroup group )
            {
                var nameBoxStart = CheckMarkSize;
                var flag         = Mod!.Settings.Settings[ group.GroupName ];

                using var raii = DrawMultiSelectorEditBegin( group );
                for( var i = 0; i < group.Options.Count; ++i )
                {
                    var opt   = group.Options[ i ];
                    var label = $"##{group.GroupName}_{i}";
                    DrawMultiSelectorCheckBox( group, i, flag, label );

                    ImGui.SameLine();
                    var newName = opt.OptionName;

                    if( nameBoxStart == CheckMarkSize )
                    {
                        nameBoxStart = ImGui.GetCursorPosX();
                    }

                    ImGui.SetNextItemWidth( MultiEditBoxWidth * ImGuiHelpers.GlobalScale );
                    if( ImGui.InputText( $"{label}_l", ref newName, 64, ImGuiInputTextFlags.EnterReturnsTrue ) )
                    {
                        if( newName.Length == 0 )
                        {
                            _modManager.RemoveModOption( i, group, Mod.Data );
                        }
                        else if( newName != opt.OptionName )
                        {
                            group.Options[ i ] = new Option()
                                { OptionName = newName, OptionDesc = opt.OptionDesc, OptionFiles = opt.OptionFiles };
                            _selector.SaveCurrentMod();
                        }

                        if( Mod!.Data.Meta.RefreshHasGroupsWithConfig() )
                        {
                            _selector.Cache.TriggerFilterReset();
                        }
                    }
                }

                DrawMultiSelectorEditAdd( group, nameBoxStart );
            }

            private void DrawSingleSelectorEditGroup( OptionGroup group )
            {
                var groupName = group.GroupName;
                if( ImGui.InputText( $"##{groupName}_add", ref groupName, 64, ImGuiInputTextFlags.EnterReturnsTrue ) )
                {
                    if( _modManager.ChangeModGroup( group.GroupName, groupName, Mod.Data ) && Mod.Data.Meta.RefreshHasGroupsWithConfig() )
                    {
                        _selector.Cache.TriggerFilterReset();
                    }
                }
            }

            private float DrawSingleSelectorEdit( OptionGroup group )
            {
                var oldSetting = Mod!.Settings.Settings[ group.GroupName ];
                var code       = oldSetting;
                if( ImGuiCustom.RenameableCombo( $"##{group.GroupName}", ref code, out var newName,
                    group.Options.Select( x => x.OptionName ).ToArray(), group.Options.Count ) )
                {
                    if( code == group.Options.Count )
                    {
                        if( newName.Length > 0 )
                        {
                            Mod.Settings.Settings[ group.GroupName ] = code;
                            group.Options.Add( new Option()
                            {
                                OptionName  = newName,
                                OptionDesc  = "",
                                OptionFiles = new Dictionary< RelPath, HashSet< GamePath > >(),
                            } );
                            _selector.SaveCurrentMod();
                        }
                    }
                    else
                    {
                        if( newName.Length == 0 )
                        {
                            _modManager.RemoveModOption( code, group, Mod.Data );
                        }
                        else
                        {
                            if( newName != group.Options[ code ].OptionName )
                            {
                                group.Options[ code ] = new Option()
                                {
                                    OptionName  = newName, OptionDesc = group.Options[ code ].OptionDesc,
                                    OptionFiles = group.Options[ code ].OptionFiles,
                                };
                                _selector.SaveCurrentMod();
                            }
                        }
                    }

                    if( Mod.Data.Meta.RefreshHasGroupsWithConfig() )
                    {
                        _selector.Cache.TriggerFilterReset();
                    }
                }

                if( code != oldSetting )
                {
                    Save();
                }

                ImGui.SameLine();
                var labelEditPos = ImGui.GetCursorPosX();
                DrawSingleSelectorEditGroup( group );

                return labelEditPos;
            }

            private void DrawAddSingleGroupField( float labelEditPos )
            {
                var newGroup = "";
                ImGui.SetCursorPosX( labelEditPos );
                if( labelEditPos == CheckMarkSize )
                {
                    ImGui.SetNextItemWidth( MultiEditBoxWidth * ImGuiHelpers.GlobalScale );
                }

                if( ImGui.InputTextWithHint( LabelNewSingleGroupEdit, "Add new Single Group...", ref newGroup, 64,
                    ImGuiInputTextFlags.EnterReturnsTrue ) )
                {
                    _modManager.ChangeModGroup( "", newGroup, Mod.Data, SelectType.Single );
                    // Adds empty group, so can not change filters.
                }
            }

            private void DrawAddMultiGroupField()
            {
                var newGroup = "";
                ImGui.SetCursorPosX( CheckMarkSize );
                ImGui.SetNextItemWidth( MultiEditBoxWidth * ImGuiHelpers.GlobalScale );
                if( ImGui.InputTextWithHint( LabelNewMultiGroup, "Add new Multi Group...", ref newGroup, 64,
                    ImGuiInputTextFlags.EnterReturnsTrue ) )
                {
                    _modManager.ChangeModGroup( "", newGroup, Mod.Data, SelectType.Multi );
                    // Adds empty group, so can not change filters.
                }
            }

            private void DrawGroupSelectorsEdit()
            {
                var labelEditPos = CheckMarkSize;
                var groups       = Meta.Groups.Values.ToArray();
                foreach( var g in groups.Where( g => g.SelectionType == SelectType.Single ) )
                {
                    labelEditPos = DrawSingleSelectorEdit( g );
                }

                DrawAddSingleGroupField( labelEditPos );

                foreach( var g in groups.Where( g => g.SelectionType == SelectType.Multi ) )
                {
                    DrawMultiSelectorEdit( g );
                }

                DrawAddMultiGroupField();
            }

            private void DrawFileSwapTabEdit()
            {
                if( !ImGui.BeginTabItem( LabelFileSwapTab ) )
                {
                    return;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );

                ImGui.SetNextItemWidth( -1 );
                if( !ImGui.BeginListBox( LabelFileSwapHeader, AutoFillSize ) )
                {
                    return;
                }

                raii.Push( ImGui.EndListBox );

                var swaps = Meta.FileSwaps.Keys.ToArray();

                ImGui.PushFont( UiBuilder.IconFont );
                var arrowWidth = ImGui.CalcTextSize( FontAwesomeIcon.LongArrowAltRight.ToIconString() ).X;
                ImGui.PopFont();

                var width = ( ImGui.GetWindowWidth() - arrowWidth - 4 * ImGui.GetStyle().ItemSpacing.X ) / 2;
                for( var idx = 0; idx < swaps.Length + 1; ++idx )
                {
                    var    key         = idx == swaps.Length ? GamePath.GenerateUnchecked( "" ) : swaps[ idx ];
                    var    value       = idx == swaps.Length ? GamePath.GenerateUnchecked( "" ) : Meta.FileSwaps[ key ];
                    string keyString   = key;
                    string valueString = value;

                    ImGui.SetNextItemWidth( width );
                    if( ImGui.InputTextWithHint( $"##swapLhs_{idx}", "Enter new file to be replaced...", ref keyString,
                        GamePath.MaxGamePathLength, ImGuiInputTextFlags.EnterReturnsTrue ) )
                    {
                        var newKey = new GamePath( keyString );
                        if( newKey.CompareTo( key ) != 0 )
                        {
                            if( idx < swaps.Length )
                            {
                                Meta.FileSwaps.Remove( key );
                            }

                            if( newKey != string.Empty )
                            {
                                Meta.FileSwaps[ newKey ] = value;
                            }

                            _selector.SaveCurrentMod();
                            _selector.ReloadCurrentMod();
                        }
                    }

                    if( idx >= swaps.Length )
                    {
                        continue;
                    }

                    ImGui.SameLine();
                    ImGuiCustom.PrintIcon( FontAwesomeIcon.LongArrowAltRight );
                    ImGui.SameLine();

                    ImGui.SetNextItemWidth( width );
                    if( ImGui.InputTextWithHint( $"##swapRhs_{idx}", "Enter new replacement path...", ref valueString,
                        GamePath.MaxGamePathLength,
                        ImGuiInputTextFlags.EnterReturnsTrue ) )
                    {
                        var newValue = new GamePath( valueString );
                        if( newValue.CompareTo( value ) != 0 )
                        {
                            Meta.FileSwaps[ key ] = newValue;
                            _selector.SaveCurrentMod();
                            _selector.Cache.TriggerListReset();
                        }
                    }
                }
            }
        }
    }
}