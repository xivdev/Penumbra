using System;
using System.Collections.Generic;
using System.Drawing.Configuration;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Plugin;
using ImGuiNET;
using Penumbra.Importer;
using Penumbra.Mod;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class Selector
        {
            [Flags]
            private enum ModFilter
            {
                Enabled                = 1 << 0,
                Disabled               = 1 << 1,
                NoConflict             = 1 << 2,
                SolvedConflict         = 1 << 3,
                UnsolvedConflict       = 1 << 4,
                HasNoMetaManipulations = 1 << 5,
                HasMetaManipulations   = 1 << 6,
                HasNoFileSwaps         = 1 << 7,
                HasFileSwaps           = 1 << 8,
                HasConfig              = 1 << 9,
                HasNoConfig            = 1 << 10,
                HasNoFiles             = 1 << 11,
                HasFiles               = 1 << 12,
            };

            private const ModFilter UnfilteredStateMods = ( ModFilter )( ( 1 << 13 ) - 1 );

            private static readonly Dictionary< ModFilter, string > ModFilterNames = new()
            {
                { ModFilter.Enabled, "Enabled" },
                { ModFilter.Disabled, "Disabled" },
                { ModFilter.NoConflict, "No Conflicts" },
                { ModFilter.SolvedConflict, "Solved Conflicts" },
                { ModFilter.UnsolvedConflict, "Unsolved Conflicts" },
                { ModFilter.HasNoMetaManipulations, "No Meta Manipulations" },
                { ModFilter.HasMetaManipulations, "Meta Manipulations" },
                { ModFilter.HasNoFileSwaps, "No File Swaps" },
                { ModFilter.HasFileSwaps, "File Swaps" },
                { ModFilter.HasNoConfig, "No Configuration" },
                { ModFilter.HasConfig, "Configuration" },
                { ModFilter.HasNoFiles, "No Files" },
                { ModFilter.HasFiles, "Files" },
            };

            private const string LabelSelectorList = "##availableModList";
            private const string LabelModFilter    = "##ModFilter";
            private const string LabelAddModPopup  = "AddMod";
            private const string LabelModHelpPopup = "Help##Selector";

            private const string TooltipModFilter =
                "Filter mods for those containing the given substring.\nEnter c:[string] to filter for mods changing specific items.\n:Enter a:[string] to filter for mods by specific authors.";

            private const string TooltipDelete   = "Delete the selected mod";
            private const string TooltipAdd      = "Add an empty mod";
            private const string DialogDeleteMod = "PenumbraDeleteMod";
            private const string ButtonYesDelete = "Yes, delete it";
            private const string ButtonNoDelete  = "No, keep it";

            private const float SelectorPanelWidth      = 240f;
            private const uint  DisabledModColor        = 0xFF666666;
            private const uint  ConflictingModColor     = 0xFFAAAAFF;
            private const uint  HandledConflictModColor = 0xFF88DDDD;

            private static readonly Vector2 SelectorButtonSizes = new( 100, 0 );
            private static readonly Vector2 HelpButtonSizes     = new( 40, 0 );

            private readonly SettingsInterface _base;
            private readonly ModManager        _modManager;
            private          string            _currentModGroup = "";

            private List< Mod.Mod >? Mods
                => _modManager.Collections.CurrentCollection.Cache?.AvailableMods;

            public Mod.Mod? Mod { get; private set; }
            private int       _index;
            private int?      _deleteIndex;
            private string    _modFilterInput   = "";
            private string    _modFilter        = "";
            private string    _modFilterChanges = "";
            private string    _modFilterAuthor  = "";
            private string[]  _modNamesLower;
            private ModFilter _stateFilter = UnfilteredStateMods;

            public Selector( SettingsInterface ui )
            {
                _base          = ui;
                _modNamesLower = Array.Empty< string >();
                _modManager    = Service< ModManager >.Get();
                ResetModNamesLower();
            }

            public void ResetModNamesLower()
            {
                _modNamesLower = Mods?.Select( m => m.Data.Meta.Name.ToLowerInvariant() ).ToArray()
                 ?? Array.Empty< string >();
            }

            public void RenameCurrentModLower( string newName )
            {
                if( _index >= 0 )
                {
                    _modNamesLower[ _index ] = newName.ToLowerInvariant();
                }
            }

            private void DrawModTrashButton()
            {
                ImGui.PushFont( UiBuilder.IconFont );

                if( ImGui.Button( FontAwesomeIcon.Trash.ToIconString(), SelectorButtonSizes ) )
                {
                    _deleteIndex = _index;
                }

                ImGui.PopFont();

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( TooltipDelete );
                }
            }

            private bool _keyboardFocus = true;

            private void DrawModAddButton()
            {
                if( ImGui.BeginPopupContextItem( LabelAddModPopup ) )
                {
                    if( _keyboardFocus )
                    {
                        ImGui.SetKeyboardFocusHere();
                        _keyboardFocus = false;
                    }

                    var newName = "";
                    if( ImGui.InputTextWithHint( "##AddMod", "New Mod Name...", ref newName, 64, ImGuiInputTextFlags.EnterReturnsTrue ) )
                    {
                        try
                        {
                            var newDir = TexToolsImport.CreateModFolder( new DirectoryInfo( _base._plugin.Configuration!.ModDirectory ),
                                newName );
                            var modMeta = new ModMeta
                            {
                                Author      = "Unknown",
                                Name        = newName,
                                Description = string.Empty,
                            };

                            var metaFile = new FileInfo( Path.Combine( newDir.FullName, "meta.json" ) );
                            modMeta.SaveToFile( metaFile );
                            _modManager.AddMod( newDir );
                            SelectModByDir( newDir.Name );
                        }
                        catch( Exception e )
                        {
                            PluginLog.Error( $"Could not create directory for new Mod {newName}:\n{e}" );
                        }

                        ImGui.CloseCurrentPopup();
                    }

                    if( ImGui.IsKeyPressed( ImGui.GetKeyIndex( ImGuiKey.Escape ) ) )
                    {
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }

                ImGui.PushFont( UiBuilder.IconFont );

                if( ImGui.Button( FontAwesomeIcon.Plus.ToIconString(), SelectorButtonSizes ) )
                {
                    _keyboardFocus = true;
                    ImGui.OpenPopup( LabelAddModPopup );
                }

                ImGui.PopFont();

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( TooltipAdd );
                }
            }

            private void DrawModHelpButton()
            {
                ImGui.PushFont( UiBuilder.IconFont );
                if( ImGui.Button( FontAwesomeIcon.QuestionCircle.ToIconString(), HelpButtonSizes ) )
                {
                    ImGui.OpenPopup( LabelModHelpPopup );
                }

                ImGui.PopFont();
            }

            private void DrawModHelpPopup()
            {
                ImGui.SetNextWindowPos( ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, Vector2.One / 2 );
                ImGui.SetNextWindowSize( new Vector2( 5 * SelectorPanelWidth, 29 * ImGui.GetTextLineHeightWithSpacing() ), ImGuiCond.Appearing );
                var _ = true;
                if( !ImGui.BeginPopupModal( LabelModHelpPopup, ref _, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove ) )
                {
                    return;
                }

                ImGui.Dummy( Vector2.UnitY * ImGui.GetTextLineHeight() );
                ImGui.Text( "Mod Selector" );
                ImGui.BulletText( "Select a mod to obtain more information." );
                ImGui.BulletText( "Mod names are colored according to their current state in the collection:"  );
                ImGui.Indent();
                ImGui.Bullet();
                ImGui.SameLine();
                ImGui.Text( "Enabled in the current collection." );
                ImGui.Bullet();
                ImGui.SameLine();
                ImGui.TextColored( ImGui.ColorConvertU32ToFloat4( DisabledModColor ), "Disabled in the current collection." );
                ImGui.Bullet();
                ImGui.SameLine();
                ImGui.TextColored( ImGui.ColorConvertU32ToFloat4( HandledConflictModColor ), "Enabled and conflicting with another enabled Mod, but on different priorities (i.e. the conflict is solved)." );
                ImGui.Bullet();
                ImGui.SameLine();
                ImGui.TextColored( ImGui.ColorConvertU32ToFloat4( ConflictingModColor ), "Enabled and conflicting with another enabled Mod on the same priority." );
                ImGui.Unindent();
                ImGui.BulletText( "Right-click a mod to enter its sort order, which is its name by default."  );
                ImGui.Indent();
                ImGui.BulletText( "A sort order differing from the mods name will not be displayed, it will just be used for ordering."  );
                ImGui.BulletText( "If the sort order string contains Forward-Slashes ('/'), the preceding substring will be turned into collapsible folders that can group mods."  );
                ImGui.BulletText( "Collapsible folders can contain further collapsible folders, so \"folder1/folder2/folder3/1\" will produce 3 folders\n\t\t[folder1] -> [folder2] -> [folder3] -> [ModName],\nwhere ModName will be sorted as if it was the string '1'."  );
                ImGui.Unindent();
                ImGui.BulletText( "Use the Filter Mods... input at the top to filter the list for mods with names containing the text." );
                ImGui.Indent();
                ImGui.BulletText( "You can enter c:[string] to filter for Changed Items instead." );
                ImGui.BulletText( "You can enter a:[string] to filter for Mod Authors instead." );
                ImGui.Unindent();
                ImGui.BulletText( "Use the expandable menu beside the input to filter for mods fulfilling specific criteria." );
                ImGui.Dummy( Vector2.UnitY * ImGui.GetTextLineHeight() );
                ImGui.Text( "Mod Management"  );
                ImGui.BulletText( "You can delete the currently selected mod with the trashcan button." );
                ImGui.BulletText( "You can add a completely empty mod with the plus button." );
                ImGui.BulletText( "You can import TTMP-based mods in the import tab." );
                ImGui.BulletText( "You can import penumbra-based mods by moving the corresponding folder into your mod directory in a file explorer, then rediscovering mods." );
                ImGui.BulletText( "If you enable Advanced Options in the Settings tab, you can toggle Edit Mode to manipulate your selected mod even further."  );
                ImGui.Dummy( Vector2.UnitY * ImGui.GetTextLineHeight()  );
                ImGui.Dummy( Vector2.UnitX * 2 * SelectorPanelWidth  );
                ImGui.SameLine();
                if( ImGui.Button( "Understood", Vector2.UnitX * SelectorPanelWidth ) )
                {
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            private void DrawModsSelectorFilter()
            {
                ImGui.SetNextItemWidth( SelectorPanelWidth - 22 );
                if( ImGui.InputTextWithHint( LabelModFilter, "Filter Mods...", ref _modFilterInput, 256 ) )
                {
                    var lower = _modFilterInput.ToLowerInvariant();
                    if( lower.StartsWith( "c:" ) )
                    {
                        _modFilterChanges = lower.Substring( 2 );
                        _modFilter        = string.Empty;
                        _modFilterAuthor  = string.Empty;
                    }
                    else if( lower.StartsWith( "a:" ) )
                    {
                        _modFilterAuthor  = lower.Substring( 2 );
                        _modFilter        = string.Empty;
                        _modFilterChanges = string.Empty;
                    }
                    else
                    {
                        _modFilter        = lower;
                        _modFilterAuthor  = string.Empty;
                        _modFilterChanges = string.Empty;
                    }
                }

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( TooltipModFilter );
                }

                ImGui.SameLine();
                if( ImGui.BeginCombo( "##ModStateFilter", "",
                    ImGuiComboFlags.NoPreview | ImGuiComboFlags.PopupAlignLeft | ImGuiComboFlags.HeightLargest ) )
                {
                    var flags = ( int )_stateFilter;
                    foreach( ModFilter flag in Enum.GetValues( typeof( ModFilter ) ) )
                    {
                        ImGui.CheckboxFlags( ModFilterNames[ flag ], ref flags, ( int )flag );
                    }

                    _stateFilter = ( ModFilter )flags;

                    ImGui.EndCombo();
                }

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( "Filter mods for their activation status." );
                }
            }

            private void DrawModsSelectorButtons()
            {
                // Selector controls
                ImGui.PushStyleVar( ImGuiStyleVar.WindowPadding, ZeroVector );
                ImGui.PushStyleVar( ImGuiStyleVar.FrameRounding, 0 );

                DrawModTrashButton();
                ImGui.SameLine();
                DrawModHelpButton();
                ImGui.SameLine();
                DrawModAddButton();
                

                ImGui.PopStyleVar( 3 );

                DrawModHelpPopup();
            }

            private void DrawDeleteModal()
            {
                if( _deleteIndex == null )
                {
                    return;
                }

                ImGui.OpenPopup( DialogDeleteMod );

                var _ = true;
                ImGui.SetNextWindowPos( ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, Vector2.One / 2 );
                var ret = ImGui.BeginPopupModal( DialogDeleteMod, ref _, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration );
                if( !ret )
                {
                    return;
                }

                if( Mod == null )
                {
                    ImGui.CloseCurrentPopup();
                    ImGui.EndPopup();
                    return;
                }

                ImGui.Text( "Are you sure you want to delete the following mod:" );
                // todo: why the fuck does this become null??????
                ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
                ImGui.TextColored( new Vector4( 0.7f, 0.1f, 0.1f, 1 ), Mod.Data.Meta.Name ?? "Unknown" );
                ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() ) / 2 );

                var buttonSize = new Vector2( 120, 0 );
                if( ImGui.Button( ButtonYesDelete, buttonSize ) )
                {
                    ImGui.CloseCurrentPopup();
                    _modManager.DeleteMod( Mod.Data.BasePath );
                    ResetModNamesLower();
                    ClearSelection();
                }

                ImGui.SameLine();

                if( ImGui.Button( ButtonNoDelete, buttonSize ) )
                {
                    ImGui.CloseCurrentPopup();
                    _deleteIndex = null;
                }

                ImGui.EndPopup();
            }

            private bool CheckFlags( int count, ModFilter hasNoFlag, ModFilter hasFlag )
            {
                if( count == 0 )
                {
                    if( _stateFilter.HasFlag( hasNoFlag ) )
                    {
                        return false;
                    }
                }
                else if( _stateFilter.HasFlag( hasFlag ) )
                {
                    return false;
                }

                return true;
            }

            private bool CheckFilters( Mod.Mod mod, int modIndex )
                => ( _modFilter.Length        == 0 || _modNamesLower[ modIndex ].Contains( _modFilter ) )
                 && ( _modFilterAuthor.Length == 0 || mod.Data.Meta.Author.ToLowerInvariant().Contains( _modFilterAuthor ) )
                 && ( _modFilterChanges.Length == 0
                     || mod.Data.ChangedItems.Any( s => s.Key.ToLowerInvariant().Contains( _modFilterChanges ) ) )
                 && !CheckFlags( mod.Data.Resources.ModFiles.Count, ModFilter.HasNoFiles, ModFilter.HasFiles )
                 && !CheckFlags( mod.Data.Meta.FileSwaps.Count, ModFilter.HasNoFileSwaps, ModFilter.HasFileSwaps )
                 && !CheckFlags( mod.Data.Resources.MetaManipulations.Count, ModFilter.HasNoMetaManipulations, ModFilter.HasMetaManipulations )
                 && !CheckFlags( mod.Data.Meta.HasGroupsWithConfig ? 1 : 0, ModFilter.HasNoConfig, ModFilter.HasConfig );

            private void DrawModOrderPopup( string popupName, Mod.Mod mod, int modIndex, bool firstOpen )
            {
                if( !ImGui.BeginPopup( popupName ) )
                {
                    return;
                }

                if( ModPanel.DrawSortOrder( mod.Data, _modManager, this ) )
                {
                    ImGui.CloseCurrentPopup();
                }

                if( firstOpen )
                {
                    ImGui.SetKeyboardFocusHere( mod.Data.SortOrder.Length - 1 );
                }

                ImGui.EndPopup();
            }

            private void DrawMod( Mod.Mod mod, int modIndex )
            {
                var changedColour = false;
                if( !mod.Settings.Enabled )
                {
                    if( !_stateFilter.HasFlag( ModFilter.Disabled ) || !_stateFilter.HasFlag( ModFilter.NoConflict ) )
                    {
                        return;
                    }

                    ImGui.PushStyleColor( ImGuiCol.Text, DisabledModColor );
                    changedColour = true;
                }
                else
                {
                    if( !_stateFilter.HasFlag( ModFilter.Enabled ) )
                    {
                        return;
                    }

                    if( mod.Cache.Conflicts.Any() )
                    {
                        if( mod.Cache.Conflicts.Keys.Any( m => m.Settings.Priority == mod.Settings.Priority ) )
                        {
                            if( !_stateFilter.HasFlag( ModFilter.UnsolvedConflict ) )
                            {
                                return;
                            }

                            ImGui.PushStyleColor( ImGuiCol.Text, ConflictingModColor );
                        }
                        else
                        {
                            if( !_stateFilter.HasFlag( ModFilter.SolvedConflict ) )
                            {
                                return;
                            }

                            ImGui.PushStyleColor( ImGuiCol.Text, HandledConflictModColor );
                        }

                        changedColour = true;
                    }
                    else if( !_stateFilter.HasFlag( ModFilter.NoConflict ) )
                    {
                        return;
                    }
                }

                var selected = ImGui.Selectable( $"{mod.Data.Meta.Name}##{modIndex}", modIndex == _index );

                if( changedColour )
                {
                    ImGui.PopStyleColor();
                }

                var popupName = $"##SortOrderPopup{modIndex}";
                var firstOpen = false;
                if( ImGui.IsItemClicked( ImGuiMouseButton.Right ) )
                {
                    ImGui.OpenPopup( popupName );
                    firstOpen = true;
                }

                DrawModOrderPopup( popupName, mod, modIndex, firstOpen );

                if( selected )
                {
                    SetSelection( modIndex, mod );
                }
            }

            private bool DrawModGroup( Mod.Mod mod, ref int modIndex )
            {
                if( !CheckFilters( mod, modIndex ) )
                {
                    return true;
                }

                if( !mod.Data.SortOrder.StartsWith( _currentModGroup ) )
                {
                    var lastFolder = _currentModGroup.LastIndexOf( '/', _currentModGroup.Length - 2 );
                    _currentModGroup = lastFolder == -1 ? string.Empty : _currentModGroup.Substring( 0, lastFolder + 1 );
                    ImGui.TreePop();
                    return false;
                }

                var nextFolder = mod.Data.SortOrder.IndexOf( '/', _currentModGroup.Length );
                if( nextFolder == -1 )
                {
                    DrawMod( mod, modIndex );
                }
                else
                {
                    var mods = Mods!;
                    var folderLabel =
                        $"{mod.Data.SortOrder.Substring( _currentModGroup.Length, nextFolder - _currentModGroup.Length )}##{_currentModGroup}";
                    _currentModGroup = mod.Data.SortOrder.Substring( 0, nextFolder + 1 );

                    if( ImGui.TreeNodeEx( folderLabel ) )
                    {
                        for( ; modIndex < mods.Count; ++modIndex )
                        {
                            if( !DrawModGroup( mods[ modIndex ], ref modIndex ) )
                            {
                                return false;
                            }
                        }
                    }
                    else
                    {
                        ImGui.TreePush();
                        for( ; modIndex < mods.Count; ++modIndex )
                        {
                            if( !mods[ modIndex ].Data.SortOrder.StartsWith( _currentModGroup ) )
                            {
                                return false;
                            }
                        }
                    }

                    if( ImGui.IsItemClicked( ImGuiMouseButton.Right ) )
                    { }
                }

                return true;
            }

            private void CleanUpLastGroup()
            {
                var numFolders = _currentModGroup.Count( c => c == '/' );
                while( numFolders-- > 0 )
                {
                    ImGui.TreePop();
                }

                _currentModGroup = string.Empty;
            }

            public void Draw()
            {
                if( Mods == null )
                {
                    return;
                }

                // Selector pane
                ImGui.BeginGroup();
                ImGui.PushStyleVar( ImGuiStyleVar.ItemSpacing, ZeroVector );

                DrawModsSelectorFilter();
                // Inlay selector list
                ImGui.BeginChild( LabelSelectorList, new Vector2( SelectorPanelWidth, -ImGui.GetFrameHeightWithSpacing() ), true );

                ImGui.PushStyleVar( ImGuiStyleVar.IndentSpacing, 12.5f );
                for( var modIndex = 0; modIndex < Mods!.Count; )
                {
                    if( DrawModGroup( Mods[ modIndex ], ref modIndex ) )
                    {
                        ++modIndex;
                    }
                }

                CleanUpLastGroup();
                ImGui.PopStyleVar();

                ImGui.EndChild();

                DrawModsSelectorButtons();
                ImGui.EndGroup();

                DrawDeleteModal();
            }

            private void SetSelection( int idx, Mod.Mod? info )
            {
                Mod = info;
                if( idx != _index )
                {
                    _base._menu.InstalledTab.ModPanel.Details.ResetState();
                }

                _index       = idx;
                _deleteIndex = null;
            }

            private void SetSelection( int idx )
            {
                if( idx >= ( Mods?.Count ?? 0 ) )
                {
                    idx = -1;
                }

                if( idx < 0 )
                {
                    SetSelection( 0, null );
                }
                else
                {
                    SetSelection( idx, Mods![ idx ] );
                }
            }

            public void ReloadSelection()
                => SetSelection( _index, Mods![ _index ] );

            public void ClearSelection()
                => SetSelection( -1 );

            public void SelectModByName( string name )
            {
                var idx = Mods?.FindIndex( mod => mod.Data.Meta.Name == name ) ?? -1;
                SetSelection( idx );
            }

            public void SelectModByDir( string name )
            {
                var idx = Mods?.FindIndex( mod => mod.Data.BasePath.Name == name ) ?? -1;
                SetSelection( idx );
            }

            public void ReloadCurrentMod( bool reloadMeta = false, bool recomputeMeta = false )
            {
                if( Mod == null )
                {
                    return;
                }

                if( _index >= 0 && _modManager.UpdateMod( Mod.Data, reloadMeta, recomputeMeta ) )
                {
                    ResetModNamesLower();
                    SelectModByDir( Mod.Data.BasePath.Name );
                    _base._menu.InstalledTab.ModPanel.Details.ResetState();
                }
            }

            public void SaveCurrentMod()
                => Mod?.Data.SaveMeta();
        }
    }
}