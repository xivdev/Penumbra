using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
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
        // Constants
        private partial class Selector
        {
            private const string LabelSelectorList = "##availableModList";
            private const string LabelModFilter    = "##ModFilter";
            private const string LabelAddModPopup  = "AddModPopup";
            private const string LabelModHelpPopup = "Help##Selector";

            private const string TooltipModFilter =
                "Filter mods for those containing the given substring.\nEnter c:[string] to filter for mods changing specific items.\nEnter a:[string] to filter for mods by specific authors.";

            private const string TooltipDelete   = "Delete the selected mod";
            private const string TooltipAdd      = "Add an empty mod";
            private const string DialogDeleteMod = "PenumbraDeleteMod";
            private const string ButtonYesDelete = "Yes, delete it";
            private const string ButtonNoDelete  = "No, keep it";

            private const float SelectorPanelWidth = 240f;

            private static readonly Vector2 SelectorButtonSizes = new( 100, 0 );
            private static readonly Vector2 HelpButtonSizes     = new( 40, 0 );
        }

        // Buttons
        private partial class Selector
        {
            // === Delete ===
            private int? _deleteIndex;

            private void DrawModTrashButton()
            {
                ImGui.PushFont( UiBuilder.IconFont );

                if( ImGui.Button( FontAwesomeIcon.Trash.ToIconString(), SelectorButtonSizes * _selectorScalingFactor ) && _index >= 0 )
                {
                    _deleteIndex = _index;
                }

                ImGui.PopFont();

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( TooltipDelete );
                }
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
                    _deleteIndex = null;
                    ImGui.CloseCurrentPopup();
                    ImGui.EndPopup();
                    return;
                }

                ImGui.Text( "Are you sure you want to delete the following mod:" );
                ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
                ImGui.TextColored( new Vector4( 0.7f, 0.1f, 0.1f, 1 ), Mod.Data.Meta.Name );
                ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() ) / 2 );

                var buttonSize = new Vector2( 120, 0 );
                if( ImGui.Button( ButtonYesDelete, buttonSize ) )
                {
                    ImGui.CloseCurrentPopup();
                    var mod = Mod;
                    Cache.RemoveMod( mod );
                    _modManager.DeleteMod( mod.Data.BasePath );
                    ModFileSystem.InvokeChange();
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

            // === Add ===
            private bool _modAddKeyboardFocus = true;

            private void DrawModAddButton()
            {
                ImGui.PushFont( UiBuilder.IconFont );

                if( ImGui.Button( FontAwesomeIcon.Plus.ToIconString(), SelectorButtonSizes * _selectorScalingFactor ) )
                {
                    _modAddKeyboardFocus = true;
                    ImGui.OpenPopup( LabelAddModPopup );
                }

                ImGui.PopFont();

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( TooltipAdd );
                }

                DrawModAddPopup();
            }

            private void DrawModAddPopup()
            {
                if( !ImGui.BeginPopup( LabelAddModPopup ) )
                {
                    return;
                }

                if( _modAddKeyboardFocus )
                {
                    ImGui.SetKeyboardFocusHere();
                    _modAddKeyboardFocus = false;
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
                            Name        = newName.Replace( '/', '\\' ),
                            Description = string.Empty,
                        };

                        var metaFile = new FileInfo( Path.Combine( newDir.FullName, "meta.json" ) );
                        modMeta.SaveToFile( metaFile );
                        _modManager.AddMod( newDir );
                        ModFileSystem.InvokeChange();
                        SelectModOnUpdate( newDir.Name );
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

            // === Help ===
            private void DrawModHelpButton()
            {
                ImGui.PushFont( UiBuilder.IconFont );
                if( ImGui.Button( FontAwesomeIcon.QuestionCircle.ToIconString(), HelpButtonSizes * _selectorScalingFactor ) )
                {
                    ImGui.OpenPopup( LabelModHelpPopup );
                }

                ImGui.PopFont();
            }

            private static void DrawModHelpPopup()
            {
                ImGui.SetNextWindowPos( ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, Vector2.One / 2 );
                ImGui.SetNextWindowSize( new Vector2( 5 * SelectorPanelWidth, 33 * ImGui.GetTextLineHeightWithSpacing() ),
                    ImGuiCond.Appearing );
                var _ = true;
                if( !ImGui.BeginPopupModal( LabelModHelpPopup, ref _, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove ) )
                {
                    return;
                }

                ImGui.Dummy( Vector2.UnitY * ImGui.GetTextLineHeight() );
                ImGui.Text( "Mod Selector" );
                ImGui.BulletText( "Select a mod to obtain more information." );
                ImGui.BulletText( "Mod names are colored according to their current state in the collection:" );
                ImGui.Indent();
                ImGui.Bullet();
                ImGui.SameLine();
                ImGui.Text( "Enabled in the current collection." );
                ImGui.Bullet();
                ImGui.SameLine();
                ImGui.TextColored( ImGui.ColorConvertU32ToFloat4( ModListCache.DisabledModColor ), "Disabled in the current collection." );
                ImGui.Bullet();
                ImGui.SameLine();
                ImGui.TextColored( ImGui.ColorConvertU32ToFloat4( ModListCache.HandledConflictModColor ),
                    "Enabled and conflicting with another enabled Mod, but on different priorities (i.e. the conflict is solved)." );
                ImGui.Bullet();
                ImGui.SameLine();
                ImGui.TextColored( ImGui.ColorConvertU32ToFloat4( ModListCache.ConflictingModColor ),
                    "Enabled and conflicting with another enabled Mod on the same priority." );
                ImGui.Unindent();
                ImGui.BulletText( "Right-click a mod to enter its sort order, which is its name by default." );
                ImGui.Indent();
                ImGui.BulletText( "A sort order differing from the mods name will not be displayed, it will just be used for ordering." );
                ImGui.BulletText(
                    "If the sort order string contains Forward-Slashes ('/'), the preceding substring will be turned into collapsible folders that can group mods." );
                ImGui.BulletText(
                    "Collapsible folders can contain further collapsible folders, so \"folder1/folder2/folder3/1\" will produce 3 folders\n"
                  + "\t\t[folder1] -> [folder2] -> [folder3] -> [ModName],\n"
                  + "where ModName will be sorted as if it was the string '1'." );
                ImGui.Unindent();
                ImGui.BulletText(
                    "You can drag and drop mods and subfolders into existing folders. Dropping them onto mods is the same as dropping them onto the parent of the mod." );
                ImGui.BulletText( "Right-clicking a folder opens a context menu." );
                ImGui.Indent();
                ImGui.BulletText(
                    "You can rename folders in the context menu. Leave the text blank and press enter to merge the folder with its parent." );
                ImGui.BulletText( "You can also enable or disable all descendant mods of a folder." );
                ImGui.Unindent();
                ImGui.BulletText( "Use the Filter Mods... input at the top to filter the list for mods with names containing the text." );
                ImGui.Indent();
                ImGui.BulletText( "You can enter c:[string] to filter for Changed Items instead." );
                ImGui.BulletText( "You can enter a:[string] to filter for Mod Authors instead." );
                ImGui.Unindent();
                ImGui.BulletText( "Use the expandable menu beside the input to filter for mods fulfilling specific criteria." );
                ImGui.Dummy( Vector2.UnitY * ImGui.GetTextLineHeight() );
                ImGui.Text( "Mod Management" );
                ImGui.BulletText( "You can delete the currently selected mod with the trashcan button." );
                ImGui.BulletText( "You can add a completely empty mod with the plus button." );
                ImGui.BulletText( "You can import TTMP-based mods in the import tab." );
                ImGui.BulletText(
                    "You can import penumbra-based mods by moving the corresponding folder into your mod directory in a file explorer, then rediscovering mods." );
                ImGui.BulletText(
                    "If you enable Advanced Options in the Settings tab, you can toggle Edit Mode to manipulate your selected mod even further." );
                ImGui.Dummy( Vector2.UnitY * ImGui.GetTextLineHeight() );
                ImGui.Dummy( Vector2.UnitX * 2 * SelectorPanelWidth );
                ImGui.SameLine();
                if( ImGui.Button( "Understood", Vector2.UnitX * SelectorPanelWidth ) )
                {
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            // === Main ===
            private void DrawModsSelectorButtons()
            {
                // Selector controls
                ImGui.PushStyleVar( ImGuiStyleVar.WindowPadding, ZeroVector );
                ImGui.PushStyleVar( ImGuiStyleVar.FrameRounding, 0 );

                DrawModAddButton();
                ImGui.SameLine();
                DrawModHelpButton();
                ImGui.SameLine();
                DrawModTrashButton();

                ImGui.PopStyleVar( 2 );
            }
        }

        // Filters
        private partial class Selector
        {
            private string _modFilterInput = "";

            private void DrawTextFilter()
            {
                ImGui.SetNextItemWidth( SelectorPanelWidth * _selectorScalingFactor - 22 );
                var tmp = _modFilterInput;
                if( ImGui.InputTextWithHint( LabelModFilter, "Filter Mods...", ref tmp, 256 ) && _modFilterInput != tmp )
                {
                    Cache.SetTextFilter( tmp );
                    _modFilterInput = tmp;
                }

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( TooltipModFilter );
                }
            }

            private void DrawToggleFilter()
            {
                if( ImGui.BeginCombo( "##ModStateFilter", "",
                    ImGuiComboFlags.NoPreview | ImGuiComboFlags.PopupAlignLeft | ImGuiComboFlags.HeightLargest ) )
                {
                    var flags = ( int )Cache.StateFilter;
                    foreach( ModFilter flag in Enum.GetValues( typeof( ModFilter ) ) )
                    {
                        ImGui.CheckboxFlags( flag.ToName(), ref flags, ( int )flag );
                    }

                    Cache.StateFilter = ( ModFilter )flags;

                    ImGui.EndCombo();
                }

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( "Filter mods for their activation status." );
                }
            }

            private void DrawModsSelectorFilter()
            {
                ImGui.PushStyleVar( ImGuiStyleVar.ItemSpacing, ZeroVector );
                DrawTextFilter();
                ImGui.SameLine();
                DrawToggleFilter();
                ImGui.PopStyleVar();
            }
        }

        // Drag'n Drop
        private partial class Selector
        {
            private const string DraggedModLabel    = "ModIndex";
            private const string DraggedFolderLabel = "FolderName";

            private readonly IntPtr _dragDropPayload = Marshal.AllocHGlobal( 4 );

            private static unsafe bool IsDropping( string name )
                => ImGui.AcceptDragDropPayload( name ).NativePtr != null;

            private void DragDropTarget( ModFolder folder )
            {
                if( !ImGui.BeginDragDropTarget() )
                {
                    return;
                }

                if( IsDropping( DraggedModLabel ) )
                {
                    var payload  = ImGui.GetDragDropPayload();
                    var modIndex = Marshal.ReadInt32( payload.Data );
                    var mod      = Cache.GetMod( modIndex ).Item1;
                    if( mod != null )
                    {
                        mod.Data.Move( folder );
                    }
                }
                else if( IsDropping( DraggedFolderLabel ) )
                {
                    var payload    = ImGui.GetDragDropPayload();
                    var folderName = Marshal.PtrToStringUni( payload.Data );
                    if( ModFileSystem.Find( folderName!, out var droppedFolder )
                     && !ReferenceEquals( droppedFolder, folder )
                     && !folder.FullName.StartsWith( folderName, StringComparison.InvariantCultureIgnoreCase ) )
                    {
                        droppedFolder.Move( folder );
                    }
                }

                ImGui.EndDragDropTarget();
            }

            private void DragDropSourceFolder( ModFolder folder )
            {
                if( !ImGui.BeginDragDropSource() )
                {
                    return;
                }

                var folderName = folder.FullName;
                var ptr        = Marshal.StringToHGlobalUni( folderName );
                ImGui.SetDragDropPayload( DraggedFolderLabel, ptr, ( uint )( folderName.Length + 1 ) * 2 );
                ImGui.Text( $"Moving {folderName}..." );
                ImGui.EndDragDropSource();
            }

            private void DragDropSourceMod( int modIndex, string modName )
            {
                if( !ImGui.BeginDragDropSource() )
                {
                    return;
                }

                Marshal.WriteInt32( _dragDropPayload, modIndex );
                ImGui.SetDragDropPayload( "ModIndex", _dragDropPayload, 4 );
                ImGui.Text( $"Moving {modName}..." );
                ImGui.EndDragDropSource();
            }

            ~Selector()
                => Marshal.FreeHGlobal( _dragDropPayload );
        }

        // Selection
        private partial class Selector
        {
            public Mod.Mod? Mod { get; private set; }
            private int    _index;
            private string _nextDir = string.Empty;

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
                if( idx >= Cache.Count )
                {
                    idx = -1;
                }

                if( idx < 0 )
                {
                    SetSelection( 0, null );
                }
                else
                {
                    SetSelection( idx, Cache.GetMod( idx ).Item1 );
                }
            }

            public void ReloadSelection()
                => SetSelection( _index, Cache.GetMod( _index ).Item1 );

            public void ClearSelection()
                => SetSelection( -1 );

            public void SelectModOnUpdate( string directory )
                => _nextDir = directory;

            public void SelectModByDir( string name )
            {
                var (mod, idx) = Cache.GetModByBasePath( name );
                SetSelection( idx, mod );
            }

            public void ReloadCurrentMod( bool reloadMeta = false, bool recomputeMeta = false )
            {
                if( Mod == null )
                {
                    return;
                }

                if( _index >= 0 && _modManager.UpdateMod( Mod.Data, reloadMeta, recomputeMeta ) )
                {
                    SelectModOnUpdate( Mod.Data.BasePath.Name );
                    _base._menu.InstalledTab.ModPanel.Details.ResetState();
                }
            }

            public void SaveCurrentMod()
                => Mod?.Data.SaveMeta();
        }

        // Right-Clicks
        private partial class Selector
        {
            // === Mod ===
            private void DrawModOrderPopup( string popupName, Mod.Mod mod, bool firstOpen )
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
                    ImGui.SetKeyboardFocusHere( mod.Data.SortOrder.FullPath.Length - 1 );
                }

                ImGui.EndPopup();
            }

            // === Folder ===
            private string _newFolderName = string.Empty;

            private void ChangeStatusOfChildren( ModFolder folder, int currentIdx, bool toWhat )
            {
                var change     = false;
                var metaManips = false;
                foreach( var _ in folder.AllMods( _modManager.Config.SortFoldersFirst ) )
                {
                    var (mod, _, _) = Cache.GetMod( currentIdx++ );
                    if( mod != null )
                    {
                        change                |= mod.Settings.Enabled != toWhat;
                        mod!.Settings.Enabled =  toWhat;
                        metaManips            |= mod.Data.Resources.MetaManipulations.Count > 0;
                    }
                }

                if( !change )
                {
                    return;
                }

                Cache.TriggerFilterReset();
                var collection = _modManager.Collections.CurrentCollection;
                if( collection.Cache != null )
                {
                    collection.CalculateEffectiveFileList( _modManager.TempPath, metaManips,
                        collection == _modManager.Collections.ActiveCollection );
                }

                collection.Save( _base._plugin.PluginInterface );
            }

            private void DrawRenameFolderInput( ModFolder folder )
            {
                ImGui.SetNextItemWidth( 150 );
                if( !ImGui.InputTextWithHint( "##NewFolderName", "Rename Folder...", ref _newFolderName, 64,
                    ImGuiInputTextFlags.EnterReturnsTrue ) )
                {
                    return;
                }

                if( _newFolderName.Any() )
                {
                    folder.Rename( _newFolderName );
                }
                else
                {
                    folder.Merge( folder.Parent! );
                }

                _newFolderName = string.Empty;
            }

            private void DrawFolderContextMenu( ModFolder folder, int currentIdx, string treeName )
            {
                if( ImGui.BeginPopup( treeName ) )
                {
                    if( ImGui.MenuItem( "Enable All Descendants" ) )
                    {
                        ChangeStatusOfChildren( folder, currentIdx, true );
                    }

                    if( ImGui.MenuItem( "Disable All Descendants" ) )
                    {
                        ChangeStatusOfChildren( folder, currentIdx, false );
                    }

                    ImGui.Dummy( Vector2.UnitY * 10 );
                    DrawRenameFolderInput( folder );

                    ImGui.EndPopup();
                }
            }
        }

        // Main-Interface
        private partial class Selector
        {
            private readonly SettingsInterface _base;
            private readonly ModManager        _modManager;
            public readonly  ModListCache      Cache;

            private float _selectorScalingFactor = 1;

            public Selector( SettingsInterface ui )
            {
                _base       = ui;
                _modManager = Service< ModManager >.Get();
                Cache       = new ModListCache( _modManager );
            }

            private void DrawCollectionButton( string label, string tooltipLabel, float size, ModCollection collection )
            {
                if( collection == ModCollection.Empty
                 || collection == _modManager.Collections.CurrentCollection )
                {
                    ImGui.PushStyleVar( ImGuiStyleVar.Alpha, 0.5f );
                    ImGui.Button( label, Vector2.UnitX * size );
                    ImGui.PopStyleVar();
                }
                else if( ImGui.Button( label, Vector2.UnitX * size ) )
                {
                    _base._menu.CollectionsTab.SetCurrentCollection( collection );
                }

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip(
                        $"Switches to the currently set {tooltipLabel} collection, if it is not set to None and it is not the current collection already." );
                }
            }

            private void DrawHeaderBar()
            {
                const float size = 200;
                DrawModsSelectorFilter();
                var textSize  = ImGui.CalcTextSize( TabCollections.LabelCurrentCollection ).X + ImGui.GetStyle().ItemInnerSpacing.X;
                var comboSize = size * ImGui.GetIO().FontGlobalScale;
                var offset    = comboSize + textSize;

                var buttonSize = ( ImGui.GetWindowContentRegionWidth()
                      - offset
                      - SelectorPanelWidth * _selectorScalingFactor
                      - 4                  * ImGui.GetStyle().ItemSpacing.X )
                  / 2;
                ImGui.SameLine();
                DrawCollectionButton( "Default", "default", buttonSize, _modManager.Collections.DefaultCollection );

                ImGui.SameLine();
                DrawCollectionButton( "Forced", "forced", buttonSize, _modManager.Collections.ForcedCollection );

                ImGui.SameLine();
                ImGui.SetNextItemWidth( comboSize );
                ImGui.PushStyleVar( ImGuiStyleVar.ItemSpacing, Vector2.Zero );
                _base._menu.CollectionsTab.DrawCurrentCollectionSelector( false );
                ImGui.PopStyleVar();
            }

            private void DrawFolderContent( ModFolder folder, ref int idx )
            {
                // Collection may be manipulated.
                foreach( var item in folder.GetItems( _modManager.Config.SortFoldersFirst ).ToArray() )
                {
                    if( item is ModFolder sub )
                    {
                        var (visible, enabled) = Cache.GetFolder( sub );
                        if( visible )
                        {
                            DrawModFolder( sub, ref idx );
                        }
                        else
                        {
                            idx += sub.TotalDescendantMods();
                        }
                    }
                    else if( item is ModData _ )
                    {
                        var (mod, visible, color) = Cache.GetMod( idx );
                        if( mod != null && visible )
                        {
                            DrawMod( mod, idx++, color );
                        }
                        else
                        {
                            ++idx;
                        }
                    }
                }
            }

            private void DrawModFolder( ModFolder folder, ref int idx )
            {
                var treeName = $"{folder.Name}##{folder.FullName}";
                var open     = ImGui.TreeNodeEx( treeName );
                if( ImGui.IsItemClicked( ImGuiMouseButton.Right ) )
                {
                    _newFolderName = string.Empty;
                    ImGui.OpenPopup( treeName );
                }

                DrawFolderContextMenu( folder, idx, treeName );
                DragDropTarget( folder );
                DragDropSourceFolder( folder );

                if( open )
                {
                    DrawFolderContent( folder, ref idx );
                    ImGui.TreePop();
                }
                else
                {
                    idx += folder.TotalDescendantMods();
                }
            }

            private void DrawMod( Mod.Mod mod, int modIndex, uint color )
            {
                if( color != 0 )
                {
                    ImGui.PushStyleColor( ImGuiCol.Text, color );
                }

                var selected = ImGui.Selectable( $"{mod.Data.Meta.Name}##{modIndex}", modIndex == _index );

                if( color != 0 )
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

                DragDropTarget( mod.Data.SortOrder.ParentFolder );
                DragDropSourceMod( modIndex, mod.Data.Meta.Name );

                DrawModOrderPopup( popupName, mod, firstOpen );

                if( selected )
                {
                    SetSelection( modIndex, mod );
                }
            }

            public void Draw()
            {
                if( Cache.Update() )
                {
                    if( _nextDir.Any() )
                    {
                        SelectModByDir( _nextDir );
                        _nextDir = string.Empty;
                    }
                    else if( Mod != null )
                    {
                        SelectModByDir( Mod.Data.BasePath.Name );
                    }
                }

                try
                {
                    _selectorScalingFactor = _base._plugin.Configuration.ScaleModSelector
                        ? ImGui.GetWindowWidth() / SettingsMenu.MinSettingsSize.X
                        : 1f;
                    // Selector pane
                    DrawHeaderBar();
                    ImGui.PushStyleVar( ImGuiStyleVar.ItemSpacing, Vector2.Zero );
                    ImGui.BeginGroup();
                    // Inlay selector list
                    ImGui.BeginChild( LabelSelectorList,
                        new Vector2( SelectorPanelWidth * _selectorScalingFactor, -ImGui.GetFrameHeightWithSpacing() ),
                        true, ImGuiWindowFlags.HorizontalScrollbar );

                    ImGui.PushStyleVar( ImGuiStyleVar.IndentSpacing, 12.5f );

                    var modIndex = 0;
                    DrawFolderContent( _modManager.StructuredMods, ref modIndex );
                    ImGui.PopStyleVar();

                    ImGui.EndChild();

                    DrawModsSelectorButtons();
                    ImGui.EndGroup();
                }
                finally
                {
                    ImGui.PopStyleVar();
                }

                DrawModHelpPopup();

                DrawDeleteModal();
            }
        }
    }
}