using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Plugin;
using ImGuiNET;
using ImGuiScene;
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
            private const string TooltipModFilter  = "Filter mods for those containing the given substring.";
            private const string TooltipDelete     = "Delete the selected mod";
            private const string TooltipAdd        = "Add an empty mod";
            private const string DialogDeleteMod   = "PenumbraDeleteMod";
            private const string ButtonYesDelete   = "Yes, delete it";
            private const string ButtonNoDelete    = "No, keep it";

            private const float SelectorPanelWidth      = 240f;
            private const uint  DisabledModColor        = 0xFF666666;
            private const uint  ConflictingModColor     = 0xFFAAAAFF;
            private const uint  HandledConflictModColor = 0xFF88DDDD;

            private static readonly Vector2 SelectorButtonSizes = new( 120, 0 );

            private readonly SettingsInterface _base;
            private readonly ModManager        _modManager;
            private          string            _currentModGroup = "";

            private List< Mod.Mod >? Mods
                => _modManager.Collections.CurrentCollection.Cache?.AvailableMods;

            public Mod.Mod? Mod { get; private set; }
            private int       _index;
            private int?      _deleteIndex;
            private string    _modFilter = "";
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

            private void DrawModsSelectorFilter()
            {
                ImGui.SetNextItemWidth( SelectorButtonSizes.X * 2 - 22 );
                var tmp = _modFilter;
                if( ImGui.InputTextWithHint( LabelModFilter, "Filter Mods...", ref tmp, 256 ) )
                {
                    _modFilter = tmp.ToLowerInvariant();
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
                DrawModAddButton();

                ImGui.PopStyleVar( 3 );
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
                => ( _modFilter.Length <= 0 || _modNamesLower[ modIndex ].Contains( _modFilter ) )
                 && !CheckFlags( mod.Data.Resources.ModFiles.Count, ModFilter.HasNoFiles, ModFilter.HasFiles )
                 && !CheckFlags( mod.Data.Meta.FileSwaps.Count, ModFilter.HasNoFileSwaps, ModFilter.HasFileSwaps )
                 && !CheckFlags( mod.Data.Resources.MetaManipulations.Count, ModFilter.HasNoMetaManipulations, ModFilter.HasMetaManipulations )
                 && !CheckFlags( mod.Data.Meta.HasGroupsWithConfig ? 1 : 0, ModFilter.HasNoConfig, ModFilter.HasConfig );

            public void DrawMod( Mod.Mod mod, int modIndex )
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
                    var count      = _currentModGroup.Length - 2;
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
                        $"{mod.Data.SortOrder.Substring( _currentModGroup.Length, nextFolder - _currentModGroup.Length )}##{modIndex}_{_currentModGroup.Length}";
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
                }

                return true;
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

                ImGui.PushStyleVar( ImGuiStyleVar.IndentSpacing, 10 );
                for( var modIndex = 0; modIndex < Mods!.Count; )
                {
                    if( DrawModGroup( Mods[ modIndex ], ref modIndex ) )
                    {
                        ++modIndex;
                    }
                }

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

            public void ReloadCurrentMod( bool recomputeMeta = false )
            {
                if( Mod == null )
                {
                    return;
                }

                if( _index >= 0 && _modManager.UpdateMod( Mod.Data, recomputeMeta ) )
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