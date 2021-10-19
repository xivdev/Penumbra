using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using Lumina.Data.Parsing;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Util;
using Penumbra.Meta;
using Penumbra.Mod;
using Penumbra.Mods;
using Penumbra.Structs;
using Penumbra.UI.Custom;
using Penumbra.Util;
using ImGui = ImGuiNET.ImGui;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private partial class PluginDetails
        {
            private const string LabelPluginDetails      = "PenumbraPluginDetails";
            private const string LabelAboutTab           = "About";
            private const string LabelChangedItemsTab    = "Changed Items";
            private const string LabelChangedItemsHeader = "##changedItems";
            private const string LabelConflictsTab       = "Mod Conflicts";
            private const string LabelConflictsHeader    = "##conflicts";
            private const string LabelFileSwapTab        = "File Swaps";
            private const string LabelFileSwapHeader     = "##fileSwaps";
            private const string LabelFileListTab        = "Files";
            private const string LabelFileListHeader     = "##fileList";
            private const string LabelGroupSelect        = "##groupSelect";
            private const string LabelOptionSelect       = "##optionSelect";
            private const string LabelConfigurationTab   = "Configuration";

            private const string TooltipFilesTab =
                "Green files replace their standard game path counterpart (not in any option) or are in all options of a Single-Select option.\n"
              + "Yellow files are restricted to some options.";

            private const float OptionSelectionWidth = 140f;
            private const float CheckMarkSize        = 50f;
            private const uint  ColorDarkGreen       = 0xFF00A000;
            private const uint  ColorGreen           = 0xFF00C800;
            private const uint  ColorYellow          = 0xFF00C8C8;
            private const uint  ColorDarkRed         = 0xFF0000A0;
            private const uint  ColorRed             = 0xFF0000C8;


            private bool         _editMode;
            private int          _selectedGroupIndex;
            private OptionGroup? _selectedGroup;
            private int          _selectedOptionIndex;
            private Option?      _selectedOption;
            private string       _currentGamePaths = "";

            private (FileInfo name, bool selected, uint color, RelPath relName)[]? _fullFilenameList;

            private readonly Selector          _selector;
            private readonly SettingsInterface _base;
            private readonly ModManager        _modManager;

            private void SelectGroup( int idx )
            {
                // Not using the properties here because we need it to be not null forgiving in this case.
                var numGroups = _selector.Mod?.Data.Meta.Groups.Count ?? 0;
                _selectedGroupIndex = idx;
                if( _selectedGroupIndex >= numGroups )
                {
                    _selectedGroupIndex = 0;
                }

                if( numGroups > 0 )
                {
                    _selectedGroup = Meta.Groups.ElementAt( _selectedGroupIndex ).Value;
                }
                else
                {
                    _selectedGroup = null;
                }
            }

            private void SelectGroup()
                => SelectGroup( _selectedGroupIndex );

            private void SelectOption( int idx )
            {
                _selectedOptionIndex = idx;
                if( _selectedOptionIndex >= _selectedGroup?.Options.Count )
                {
                    _selectedOptionIndex = 0;
                }

                if( _selectedGroup?.Options.Count > 0 )
                {
                    _selectedOption = ( ( OptionGroup )_selectedGroup ).Options[ _selectedOptionIndex ];
                }
                else
                {
                    _selectedOption = null;
                }
            }

            private void SelectOption()
                => SelectOption( _selectedOptionIndex );

            public void ResetState()
            {
                _fullFilenameList = null;
                SelectGroup();
                SelectOption();
            }

            public PluginDetails( SettingsInterface ui, Selector s )
            {
                _base     = ui;
                _selector = s;
                ResetState();
                _modManager = Service< ModManager >.Get();
            }

            // This is only drawn when we have a mod selected, so we can forgive nulls.
            private Mod.Mod Mod
                => _selector.Mod!;

            private ModMeta Meta
                => Mod.Data.Meta;

            private void Save()
            {
                _modManager.Collections.CurrentCollection.Save();
            }

            private void DrawAboutTab()
            {
                if( !_editMode && Meta.Description.Length == 0 )
                {
                    return;
                }

                if( !ImGui.BeginTabItem( LabelAboutTab ) )
                {
                    return;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );

                var desc = Meta.Description;
                var flags = _editMode
                    ? ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.CtrlEnterForNewLine
                    : ImGuiInputTextFlags.ReadOnly;

                if( _editMode )
                {
                    if( ImGui.InputTextMultiline( LabelDescEdit, ref desc, 1 << 16,
                        AutoFillSize, flags ) )
                    {
                        Meta.Description = desc;
                        _selector.SaveCurrentMod();
                    }

                    ImGuiCustom.HoverTooltip( TooltipAboutEdit );
                }
                else
                {
                    ImGui.TextWrapped( desc );
                }
            }

            private void DrawChangedItemsTab()
            {
                if( Mod.Data.ChangedItems.Count == 0 || !ImGui.BeginTabItem( LabelChangedItemsTab ) )
                {
                    return;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );

                if( !ImGui.BeginListBox( LabelChangedItemsHeader, AutoFillSize ) )
                {
                    return;
                }

                raii.Push( ImGui.EndListBox );
                foreach( var (name, data) in Mod.Data.ChangedItems )
                {
                    var ret = ImGui.Selectable( name ) ? MouseButton.Left : MouseButton.None;
                    ret = ImGui.IsItemClicked( ImGuiMouseButton.Right ) ? MouseButton.Right : ret;
                    ret = ImGui.IsItemClicked( ImGuiMouseButton.Middle ) ? MouseButton.Middle : ret;

                    if( ret != MouseButton.None )
                    {
                        _base._penumbra.Api.InvokeClick( ret, data );
                    }

                    if( _base._penumbra.Api.HasTooltip && ImGui.IsItemHovered() )
                    {
                        ImGui.BeginTooltip();
                        raii.Push( ImGui.EndTooltip );
                        _base._penumbra.Api.InvokeTooltip( data );
                        raii.Pop();
                    }

                    if( data is Item it )
                    {
                        var modelId = $"({( ( Quad )it.ModelMain ).A})";
                        var offset  = ImGui.CalcTextSize( modelId ).X - ImGui.GetStyle().ItemInnerSpacing.X;
                        ImGui.SameLine(ImGui.GetWindowContentRegionWidth() - offset);
                        ImGui.TextColored( new Vector4(0.5f, 0.5f, 0.5f, 1  ), modelId );
                    }
                }
            }

            private void DrawConflictTab()
            {
                if( !Mod.Cache.Conflicts.Any() || !ImGui.BeginTabItem( LabelConflictsTab ) )
                {
                    return;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );

                ImGui.SetNextItemWidth( -1 );
                if( !ImGui.BeginListBox( LabelConflictsHeader, AutoFillSize ) )
                {
                    return;
                }

                raii.Push( ImGui.EndListBox );
                using var indent = ImGuiRaii.PushIndent( 0 );
                foreach( var (mod, (files, manipulations)) in Mod.Cache.Conflicts )
                {
                    if( ImGui.Selectable( mod.Data.Meta.Name ) )
                    {
                        _selector.SelectModByDir( mod.Data.BasePath.Name );
                    }

                    ImGui.SameLine();
                    ImGui.Text( $"(Priority {mod.Settings.Priority})" );

                    indent.Push( 15f );
                    foreach( var file in files )
                    {
                        ImGui.Selectable( file );
                    }

                    foreach( var manip in manipulations )
                    {
                        ImGui.Text( manip.IdentifierString() );
                    }

                    indent.Pop( 15f );
                }
            }

            private void DrawFileSwapTab()
            {
                if( _editMode )
                {
                    DrawFileSwapTabEdit();
                    return;
                }

                if( !Meta.FileSwaps.Any() || !ImGui.BeginTabItem( LabelFileSwapTab ) )
                {
                    return;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );

                const ImGuiTableFlags flags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollX;

                ImGui.SetNextItemWidth( -1 );
                if( !ImGui.BeginTable( LabelFileSwapHeader, 3, flags, AutoFillSize ) )
                {
                    return;
                }

                raii.Push( ImGui.EndTable );

                foreach( var (source, target) in Meta.FileSwaps )
                {
                    ImGui.TableNextColumn();
                    ImGuiCustom.CopyOnClickSelectable( source );

                    ImGui.TableNextColumn();
                    ImGuiCustom.PrintIcon( FontAwesomeIcon.LongArrowAltRight );

                    ImGui.TableNextColumn();
                    ImGuiCustom.CopyOnClickSelectable( target );

                    ImGui.TableNextRow();
                }
            }

            private void UpdateFilenameList()
            {
                if( _fullFilenameList != null )
                {
                    return;
                }

                _fullFilenameList = Mod.Data.Resources.ModFiles
                   .Select( f => ( f, false, ColorGreen, new RelPath( f, Mod.Data.BasePath ) ) ).ToArray();

                if( Meta.Groups.Count == 0 )
                {
                    return;
                }

                for( var i = 0; i < Mod.Data.Resources.ModFiles.Count; ++i )
                {
                    foreach( var group in Meta.Groups.Values )
                    {
                        var inAll = true;
                        foreach( var option in group.Options )
                        {
                            if( option.OptionFiles.ContainsKey( _fullFilenameList[ i ].relName ) )
                            {
                                _fullFilenameList[ i ].color = ColorYellow;
                            }
                            else
                            {
                                inAll = false;
                            }
                        }

                        if( inAll && group.SelectionType == SelectType.Single )
                        {
                            _fullFilenameList[ i ].color = ColorGreen;
                        }
                    }
                }
            }

            private void DrawFileListTab()
            {
                if( !ImGui.BeginTabItem( LabelFileListTab ) )
                {
                    return;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );
                ImGuiCustom.HoverTooltip( TooltipFilesTab );

                ImGui.SetNextItemWidth( -1 );
                if( ImGui.BeginListBox( LabelFileListHeader, AutoFillSize ) )
                {
                    raii.Push( ImGui.EndListBox );
                    UpdateFilenameList();
                    using var colorRaii = new ImGuiRaii.Color();
                    foreach( var (name, _, color, _) in _fullFilenameList! )
                    {
                        colorRaii.Push( ImGuiCol.Text, color );
                        ImGui.Selectable( name.FullName );
                        colorRaii.Pop();
                    }
                }
                else
                {
                    _fullFilenameList = null;
                }
            }

            private static int HandleDefaultString( GamePath[] gamePaths, out int removeFolders )
            {
                removeFolders = 0;
                var defaultIndex =
                    gamePaths.IndexOf( p => ( ( string )p ).StartsWith( TextDefaultGamePath ) );
                if( defaultIndex < 0 )
                {
                    return defaultIndex;
                }

                string path = gamePaths[ defaultIndex ];
                if( path.Length == TextDefaultGamePath.Length )
                {
                    return defaultIndex;
                }

                if( path[ TextDefaultGamePath.Length ] != '-'
                 || !int.TryParse( path.Substring( TextDefaultGamePath.Length + 1 ), out removeFolders ) )
                {
                    return -1;
                }

                return defaultIndex;
            }

            private void HandleSelectedFilesButton( bool remove )
            {
                if( _selectedOption == null )
                {
                    return;
                }

                var option = ( Option )_selectedOption;

                var gamePaths = _currentGamePaths.Split( ';' ).Select( p => new GamePath( p ) ).ToArray();
                if( gamePaths.Length == 0 || ( ( string )gamePaths[ 0 ] ).Length == 0 )
                {
                    return;
                }

                var defaultIndex = HandleDefaultString( gamePaths, out var removeFolders );
                var changed      = false;
                for( var i = 0; i < Mod.Data.Resources.ModFiles.Count; ++i )
                {
                    if( !_fullFilenameList![ i ].selected )
                    {
                        continue;
                    }

                    _fullFilenameList![ i ].selected = false;
                    var relName = _fullFilenameList[ i ].relName;
                    if( defaultIndex >= 0 )
                    {
                        gamePaths[ defaultIndex ] = relName.ToGamePath( removeFolders );
                    }

                    if( remove && option.OptionFiles.TryGetValue( relName, out var setPaths ) )
                    {
                        if( setPaths.RemoveWhere( p => gamePaths.Contains( p ) ) > 0 )
                        {
                            changed = true;
                        }

                        if( setPaths.Count == 0 && option.OptionFiles.Remove( relName ) )
                        {
                            changed = true;
                        }
                    }
                    else
                    {
                        changed = gamePaths
                           .Aggregate( changed, ( current, gamePath ) => current | option.AddFile( relName, gamePath ) );
                    }
                }

                if( changed )
                {
                    _fullFilenameList = null;
                    _selector.SaveCurrentMod();
                    // Since files may have changed, we need to recompute effective files.
                    foreach( var collection in _modManager.Collections.Collections.Values
                       .Where( c => c.Cache != null && c.Settings[ Mod!.Data.BasePath.Name ].Enabled ) )
                    {
                        collection.CalculateEffectiveFileList( _modManager.TempPath, false,
                            collection == _modManager.Collections.ActiveCollection );
                    }

                    // If the mod is enabled in the current collection, its conflicts may have changed.
                    if( Mod!.Settings.Enabled )
                    {
                        _selector.Cache.TriggerFilterReset();
                    }
                }
            }

            private void DrawAddToGroupButton()
            {
                if( ImGui.Button( ButtonAddToGroup ) )
                {
                    HandleSelectedFilesButton( false );
                }
            }

            private void DrawRemoveFromGroupButton()
            {
                if( ImGui.Button( ButtonRemoveFromGroup ) )
                {
                    HandleSelectedFilesButton( true );
                }
            }

            private void DrawGamePathInput()
            {
                ImGui.SetNextItemWidth( -1 );
                ImGui.InputTextWithHint( LabelGamePathsEditBox, "Hover for help...", ref _currentGamePaths,
                    128 );
                ImGuiCustom.HoverTooltip( TooltipGamePathsEdit );
            }

            private void DrawGroupRow()
            {
                if( _selectedGroup == null )
                {
                    SelectGroup();
                }

                if( _selectedOption == null )
                {
                    SelectOption();
                }

                if( !DrawEditGroupSelector() )
                {
                    return;
                }

                ImGui.SameLine();
                if( !DrawEditOptionSelector() )
                {
                    return;
                }

                ImGui.SameLine();
                DrawAddToGroupButton();
                ImGui.SameLine();
                DrawRemoveFromGroupButton();
                ImGui.SameLine();
                DrawGamePathInput();
            }

            private void DrawFileAndGamePaths( int idx )
            {
                void Selectable( uint colorNormal, uint colorReplace )
                {
                    var loc = _fullFilenameList![ idx ].color;
                    if( loc == colorNormal )
                    {
                        loc = colorReplace;
                    }

                    using var colors = ImGuiRaii.PushColor( ImGuiCol.Text, loc );
                    ImGui.Selectable( _fullFilenameList[ idx ].name.FullName, ref _fullFilenameList[ idx ].selected );
                }

                const float indentWidth = 30f;
                if( _selectedOption == null )
                {
                    Selectable( 0, ColorGreen );
                    return;
                }

                var fileName    = _fullFilenameList![ idx ].relName;
                var optionFiles = ( ( Option )_selectedOption ).OptionFiles;
                if( optionFiles.TryGetValue( fileName, out var gamePaths ) )
                {
                    Selectable( 0, ColorGreen );

                    using var indent   = ImGuiRaii.PushIndent( indentWidth );
                    var       tmpPaths = gamePaths.ToArray();
                    foreach( var gamePath in tmpPaths )
                    {
                        string tmp = gamePath;
                        if( ImGui.InputText( $"##{fileName}_{gamePath}", ref tmp, 128, ImGuiInputTextFlags.EnterReturnsTrue )
                         && tmp != gamePath )
                        {
                            gamePaths.Remove( gamePath );
                            if( tmp.Length > 0 )
                            {
                                gamePaths.Add( new GamePath( tmp ) );
                            }
                            else if( gamePaths.Count == 0 )
                            {
                                optionFiles.Remove( fileName );
                            }

                            _selector.SaveCurrentMod();
                            _selector.ReloadCurrentMod();
                        }
                    }
                }
                else
                {
                    Selectable( ColorYellow, ColorRed );
                }
            }

            private void DrawMultiSelectorCheckBox( OptionGroup group, int idx, int flag, string label )
            {
                var enabled    = ( flag & ( 1 << idx ) ) != 0;
                var oldEnabled = enabled;
                if( ImGui.Checkbox( label, ref enabled ) && oldEnabled != enabled )
                {
                    Mod.Settings.Settings[ group.GroupName ] ^= 1 << idx;
                    Save();
                    // If the mod is enabled, recalculate files and filters.
                    if( Mod.Settings.Enabled )
                    {
                        _base.RecalculateCurrent( Mod.Data.Resources.MetaManipulations.Count > 0 );
                    }
                }
            }

            private void DrawMultiSelector( OptionGroup group )
            {
                if( group.Options.Count == 0 )
                {
                    return;
                }

                ImGuiCustom.BeginFramedGroup( group.GroupName );
                using var raii = ImGuiRaii.DeferredEnd( ImGuiCustom.EndFramedGroup );
                for( var i = 0; i < group.Options.Count; ++i )
                {
                    DrawMultiSelectorCheckBox( group, i, Mod.Settings.Settings[ group.GroupName ],
                        $"{group.Options[ i ].OptionName}##{group.GroupName}" );
                }
            }

            private void DrawSingleSelector( OptionGroup group )
            {
                if( group.Options.Count < 2 )
                {
                    return;
                }

                var code = Mod.Settings.Settings[ group.GroupName ];
                if( ImGui.Combo( group.GroupName, ref code
                        , group.Options.Select( x => x.OptionName ).ToArray(), group.Options.Count )
                 && code != Mod.Settings.Settings[ group.GroupName ] )
                {
                    Mod.Settings.Settings[ group.GroupName ] = code;
                    Save();
                    // If the mod is enabled, recalculate files and filters.
                    if( Mod.Settings.Enabled )
                    {
                        _base.RecalculateCurrent( Mod.Data.Resources.MetaManipulations.Count > 0 );
                    }
                }
            }

            private void DrawGroupSelectors()
            {
                foreach( var g in Meta.Groups.Values.Where( g => g.SelectionType == SelectType.Single ) )
                {
                    DrawSingleSelector( g );
                }

                foreach( var g in Meta.Groups.Values.Where( g => g.SelectionType == SelectType.Multi ) )
                {
                    DrawMultiSelector( g );
                }
            }

            private void DrawConfigurationTab()
            {
                if( !_editMode && !Meta.HasGroupsWithConfig || !ImGui.BeginTabItem( LabelConfigurationTab ) )
                {
                    return;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );
                if( _editMode )
                {
                    DrawGroupSelectorsEdit();
                }
                else
                {
                    DrawGroupSelectors();
                }
            }

            private void DrawMetaManipulationsTab()
            {
                if( !_editMode && Mod.Data.Resources.MetaManipulations.Count == 0 || !ImGui.BeginTabItem( "Meta Manipulations" ) )
                {
                    return;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );

                if( !ImGui.BeginListBox( "##MetaManipulations", AutoFillSize ) )
                {
                    return;
                }

                raii.Push( ImGui.EndListBox );

                var manips  = Mod.Data.Resources.MetaManipulations;
                var changes = false;
                if( _editMode || manips.DefaultData.Count > 0 )
                {
                    if( ImGui.CollapsingHeader( "Default" ) )
                    {
                        changes = DrawMetaManipulationsTable( "##DefaultManips", manips.DefaultData, ref manips.Count );
                    }
                }

                foreach( var (groupName, group) in manips.GroupData )
                {
                    foreach( var (optionName, option) in group )
                    {
                        if( ImGui.CollapsingHeader( $"{groupName} - {optionName}" ) )
                        {
                            changes |= DrawMetaManipulationsTable( $"##{groupName}{optionName}manips", option, ref manips.Count );
                        }
                    }
                }

                if( changes )
                {
                    Mod.Data.Resources.MetaManipulations.SaveToFile( MetaCollection.FileName( Mod.Data.BasePath ) );
                    Mod.Data.Resources.SetManipulations( Meta, Mod.Data.BasePath, false );
                    _selector.ReloadCurrentMod( true, false );
                }
            }

            public void Draw( bool editMode )
            {
                _editMode = editMode;
                if( !ImGui.BeginTabBar( LabelPluginDetails ) )
                {
                    return;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabBar );
                DrawAboutTab();
                DrawChangedItemsTab();

                DrawConfigurationTab();
                if( _editMode )
                {
                    DrawFileListTabEdit();
                }
                else
                {
                    DrawFileListTab();
                }

                DrawFileSwapTab();
                DrawMetaManipulationsTab();
                DrawConflictTab();
            }
        }
    }
}