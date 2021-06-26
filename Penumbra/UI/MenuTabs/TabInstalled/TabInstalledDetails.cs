using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Interface;
using ImGuiNET;
using Penumbra.Game.Enums;
using Penumbra.Meta;
using Penumbra.Mod;
using Penumbra.Mods;
using Penumbra.Structs;
using Penumbra.Util;

namespace Penumbra.UI
{
    internal static class ListRemoveExtension
    {
        // Remove the entry at idx from the list if the new string is empty, otherwise replace it.
        public static void RemoveOrChange( this List< string > list, string newString, int idx )
        {
            if( newString.Length == 0 )
            {
                list.RemoveAt( idx );
            }
            else
            {
                list[ idx ] = newString;
            }
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
            private const uint  ColorGreen           = 0xFF00C800;
            private const uint  ColorYellow          = 0xFF00C8C8;
            private const uint  ColorRed             = 0xFF0000C8;

            private bool                           _editMode;
            private int                            _selectedGroupIndex;
            private OptionGroup?                   _selectedGroup;
            private int                            _selectedOptionIndex;
            private Option?                        _selectedOption;
            private (string label, string name)[]? _changedItemsList;
            private string                         _currentGamePaths = "";

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
                _changedItemsList = null;
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
                _modManager.Collections.CurrentCollection.Save( _base._plugin.PluginInterface! );
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

                    if( ImGui.IsItemHovered() )
                    {
                        ImGui.SetTooltip( TooltipAboutEdit );
                    }
                }
                else
                {
                    ImGui.TextWrapped( desc );
                }

                ImGui.EndTabItem();
            }

            private void DrawChangedItemsTab()
            {
                if( !_editMode && Meta.ChangedItems.Count == 0 )
                {
                    return;
                }

                var flags = _editMode
                    ? ImGuiInputTextFlags.EnterReturnsTrue
                    : ImGuiInputTextFlags.ReadOnly;

                if( ImGui.BeginTabItem( LabelChangedItemsTab ) )
                {
                    ImGui.SetNextItemWidth( -1 );
                    var changedItems = false;
                    if( ImGui.BeginListBox( LabelChangedItemsHeader, AutoFillSize ) )
                    {
                        _changedItemsList ??= Meta.ChangedItems
                           .Select( ( I, index ) => ( $"{LabelChangedItemIdx}{index}", I ) ).ToArray();

                        for( var i = 0; i < Meta.ChangedItems.Count; ++i )
                        {
                            ImGui.SetNextItemWidth( -1 );
                            if( ImGui.InputText( _changedItemsList[ i ].label, ref _changedItemsList[ i ].name, 128, flags ) )
                            {
                                Meta.ChangedItems.RemoveOrChange( _changedItemsList[ i ].name, i );
                                changedItems = true;
                                _selector.SaveCurrentMod();
                            }
                        }

                        var newItem = "";
                        if( _editMode )
                        {
                            ImGui.SetNextItemWidth( -1 );
                            if( ImGui.InputTextWithHint( LabelChangedItemNew, "Enter new changed item...", ref newItem, 128, flags )
                             && newItem.Length > 0 )
                            {
                                Meta.ChangedItems.Add( newItem );
                                changedItems = true;
                                _selector.SaveCurrentMod();
                            }
                        }

                        if( changedItems )
                        {
                            _changedItemsList = null;
                        }

                        ImGui.EndListBox();
                    }

                    ImGui.EndTabItem();
                }
                else
                {
                    _changedItemsList = null;
                }
            }

            private void DrawConflictTab()
            {
                if( !Mod.Cache.Conflicts.Any() || !ImGui.BeginTabItem( LabelConflictsTab ) )
                {
                    return;
                }

                ImGui.SetNextItemWidth( -1 );
                if( ImGui.BeginListBox( LabelConflictsHeader, AutoFillSize ) )
                {
                    foreach( var kv in Mod.Cache.Conflicts )
                    {
                        var mod = kv.Key;
                        if( ImGui.Selectable( mod.Data.Meta.Name ) )
                        {
                            _selector.SelectModByName( mod.Data.Meta.Name );
                        }

                        ImGui.SameLine();
                        ImGui.Text( $"(Priority {mod.Settings.Priority})" );

                        ImGui.Indent( 15 );
                        foreach( var file in kv.Value.Files )
                        {
                            ImGui.Selectable( file );
                        }

                        foreach( var manip in kv.Value.Manipulations )
                        {
                            ImGui.Text( manip.IdentifierString() );
                        }

                        ImGui.Unindent( 15 );
                    }

                    ImGui.EndListBox();
                }

                ImGui.EndTabItem();
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

                const ImGuiTableFlags flags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollX;

                ImGui.SetNextItemWidth( -1 );
                if( ImGui.BeginTable( LabelFileSwapHeader, 3, flags, AutoFillSize ) )
                {
                    foreach( var file in Meta.FileSwaps )
                    {
                        ImGui.TableNextColumn();
                        Custom.ImGuiCustom.CopyOnClickSelectable( file.Key );

                        ImGui.TableNextColumn();
                        ImGui.PushFont( UiBuilder.IconFont );
                        ImGui.TextUnformatted( $"{( char )FontAwesomeIcon.LongArrowAltRight}" );
                        ImGui.PopFont();

                        ImGui.TableNextColumn();
                        Custom.ImGuiCustom.CopyOnClickSelectable( file.Value );

                        ImGui.TableNextRow();
                    }

                    ImGui.EndTable();
                }

                ImGui.EndTabItem();
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

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( TooltipFilesTab );
                }

                ImGui.SetNextItemWidth( -1 );
                if( ImGui.BeginListBox( LabelFileListHeader, AutoFillSize ) )
                {
                    UpdateFilenameList();
                    foreach( var (name, _, color, _) in _fullFilenameList! )
                    {
                        ImGui.PushStyleColor( ImGuiCol.Text, color );
                        ImGui.Selectable( name.FullName );
                        ImGui.PopStyleColor();
                    }

                    ImGui.EndListBox();
                }
                else
                {
                    _fullFilenameList = null;
                }

                ImGui.EndTabItem();
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

                    var relName = _fullFilenameList[ i ].relName;
                    if( defaultIndex >= 0 )
                    {
                        gamePaths[ defaultIndex ] = new GamePath( relName, removeFolders );
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
                    _selector.SaveCurrentMod();
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
                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( TooltipGamePathsEdit );
                }
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

                    ImGui.PushStyleColor( ImGuiCol.Text, loc );
                    ImGui.Selectable( _fullFilenameList[ idx ].name.FullName, ref _fullFilenameList[ idx ].selected );
                    ImGui.PopStyleColor();
                }

                const float indent = 30f;
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

                    ImGui.Indent( indent );
                    var tmpPaths = gamePaths.ToArray();
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

                    ImGui.Unindent( indent );
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
                    if( Mod.Settings.Enabled )
                    {
                        _modManager.Collections.CurrentCollection.CalculateEffectiveFileList( Mod.Data.BasePath,
                            Mod.Data.Resources.MetaManipulations.Count > 0 );
                    }

                    Save();
                }
            }

            private void DrawMultiSelector( OptionGroup group )
            {
                if( group.Options.Count == 0 )
                {
                    return;
                }

                Custom.ImGuiCustom.BeginFramedGroup( group.GroupName );
                for( var i = 0; i < group.Options.Count; ++i )
                {
                    DrawMultiSelectorCheckBox( group, i, Mod.Settings.Settings[ group.GroupName ],
                        $"{group.Options[ i ].OptionName}##{group.GroupName}" );
                }

                Custom.ImGuiCustom.EndFramedGroup();
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
                    if( Mod.Settings.Enabled )
                    {
                        _modManager.Collections.CurrentCollection.CalculateEffectiveFileList( Mod.Data.BasePath,
                            Mod.Data.Resources.MetaManipulations.Count > 0 );
                    }

                    Save();
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
                if( !_editMode && !Meta.HasGroupsWithConfig )
                {
                    return;
                }

                if( ImGui.BeginTabItem( LabelConfigurationTab ) )
                {
                    if( _editMode )
                    {
                        DrawGroupSelectorsEdit();
                    }
                    else
                    {
                        DrawGroupSelectors();
                    }

                    ImGui.EndTabItem();
                }
            }

            private static void DrawManipulationRow( MetaManipulation manip )
            {
                ImGui.TableNextColumn();
                ImGui.Text( manip.Type.ToString() );
                ImGui.TableNextColumn();

                switch( manip.Type )
                {
                    case MetaType.Eqp:
                    {
                        ImGui.Text( manip.EqpIdentifier.Slot.IsAccessory()
                            ? ObjectType.Accessory.ToString()
                            : ObjectType.Equipment.ToString() );
                        ImGui.TableNextColumn();
                        ImGui.Text( manip.EqpIdentifier.SetId.ToString() );
                        ImGui.TableNextColumn();
                        ImGui.Text( manip.EqpIdentifier.Slot.ToString() );
                        break;
                    }
                    case MetaType.Gmp:
                    {
                        ImGui.Text( ObjectType.Equipment.ToString() );
                        ImGui.TableNextColumn();
                        ImGui.Text( manip.GmpIdentifier.SetId.ToString() );
                        ImGui.TableNextColumn();
                        ImGui.Text( EquipSlot.Head.ToString() );
                        break;
                    }
                    case MetaType.Eqdp:
                    {
                        ImGui.Text( manip.EqpIdentifier.Slot.IsAccessory()
                            ? ObjectType.Accessory.ToString()
                            : ObjectType.Equipment.ToString() );
                        ImGui.TableNextColumn();
                        ImGui.Text( manip.EqdpIdentifier.SetId.ToString() );
                        ImGui.TableNextColumn();
                        ImGui.Text( manip.EqpIdentifier.Slot.ToString() );
                        ImGui.TableNextColumn();
                        var (gender, race) = manip.EqdpIdentifier.GenderRace.Split();
                        ImGui.Text( race.ToString() );
                        ImGui.TableNextColumn();
                        ImGui.Text( gender.ToString() );
                        break;
                    }
                    case MetaType.Est:
                    {
                        ImGui.Text( manip.EstIdentifier.ObjectType.ToString() );
                        ImGui.TableNextColumn();
                        ImGui.Text( manip.EstIdentifier.PrimaryId.ToString() );
                        ImGui.TableNextColumn();
                        ImGui.Text( manip.EstIdentifier.ObjectType == ObjectType.Equipment
                            ? manip.EstIdentifier.EquipSlot.ToString()
                            : manip.EstIdentifier.BodySlot.ToString() );
                        ImGui.TableNextColumn();
                        var (gender, race) = manip.EstIdentifier.GenderRace.Split();
                        ImGui.Text( race.ToString() );
                        ImGui.TableNextColumn();
                        ImGui.Text( gender.ToString() );
                        break;
                    }
                    case MetaType.Imc:
                    {
                        ImGui.Text( manip.ImcIdentifier.ObjectType.ToString() );
                        ImGui.TableNextColumn();
                        ImGui.Text( manip.ImcIdentifier.PrimaryId.ToString() );
                        ImGui.TableNextColumn();
                        if( manip.ImcIdentifier.ObjectType == ObjectType.Accessory
                         || manip.ImcIdentifier.ObjectType == ObjectType.Equipment )
                        {
                            ImGui.Text( manip.ImcIdentifier.ObjectType == ObjectType.Equipment
                             || manip.ImcIdentifier.ObjectType         == ObjectType.Accessory
                                    ? manip.ImcIdentifier.EquipSlot.ToString()
                                    : manip.ImcIdentifier.BodySlot.ToString() );
                        }

                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        if( manip.ImcIdentifier.ObjectType != ObjectType.Equipment
                         && manip.ImcIdentifier.ObjectType != ObjectType.Accessory )
                        {
                            ImGui.Text( manip.ImcIdentifier.SecondaryId.ToString() );
                        }

                        ImGui.TableNextColumn();
                        ImGui.Text( manip.ImcIdentifier.Variant.ToString() );
                        break;
                    }
                }

                ImGui.TableSetColumnIndex( 9 );
                ImGui.Text( manip.Value.ToString() );
                ImGui.TableNextRow();
            }

            private static void DrawMetaManipulationsTable( string label, List< MetaManipulation > list )
            {
                if( list.Count == 0
                 || !ImGui.BeginTable( label, 10,
                        ImGuiTableFlags.BordersInner | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit ) )
                {
                    return;
                }

                ImGui.TableNextColumn();
                ImGui.TableHeader( $"Type##{label}" );
                ImGui.TableNextColumn();
                ImGui.TableHeader( $"Object Type##{label}" );
                ImGui.TableNextColumn();
                ImGui.TableHeader( $"Set##{label}" );
                ImGui.TableNextColumn();
                ImGui.TableHeader( $"Slot##{label}" );
                ImGui.TableNextColumn();
                ImGui.TableHeader( $"Race##{label}" );
                ImGui.TableNextColumn();
                ImGui.TableHeader( $"Gender##{label}" );
                ImGui.TableNextColumn();
                ImGui.TableHeader( $"Secondary ID##{label}" );
                ImGui.TableNextColumn();
                ImGui.TableHeader( $"Variant##{label}" );
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableHeader( $"Value##{label}" );
                ImGui.TableNextRow();
                foreach( var manip in list )
                {
                    DrawManipulationRow( manip );
                }

                ImGui.EndTable();
            }

            private void DrawMetaManipulationsTab()
            {
                if( Mod.Data.Resources.MetaManipulations.Count == 0 )
                {
                    return;
                }

                if( !ImGui.BeginTabItem( "Meta Manipulations" ) )
                {
                    return;
                }

                if( ImGui.BeginListBox( "##MetaManipulations", AutoFillSize ) )
                {
                    var manips = Mod.Data.Resources.MetaManipulations;
                    if( manips.DefaultData.Count > 0 )
                    {
                        if( ImGui.CollapsingHeader( "Default" ) )
                        {
                            DrawMetaManipulationsTable( "##DefaultManips", manips.DefaultData );
                        }
                    }

                    foreach( var group in manips.GroupData )
                    {
                        foreach( var option in @group.Value )
                        {
                            if( ImGui.CollapsingHeader( $"{@group.Key} - {option.Key}" ) )
                            {
                                DrawMetaManipulationsTable( $"##{@group.Key}{option.Key}manips", option.Value );
                            }
                        }
                    }

                    ImGui.EndListBox();
                }

                ImGui.EndTabItem();
            }

            public void Draw( bool editMode )
            {
                _editMode = editMode;
                ImGui.BeginTabBar( LabelPluginDetails );

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
                ImGui.EndTabBar();
            }
        }
    }
}