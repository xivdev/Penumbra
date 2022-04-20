using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private class ModPanel
    {
        private readonly ConfigWindow       _window;
        private          bool               _valid;
        private          bool               _emptySetting;
        private          bool               _inherited;
        private          ModFileSystem.Leaf _leaf         = null!;
        private          Mod2               _mod          = null!;
        private          ModSettings2       _settings     = null!;
        private          ModCollection      _collection   = null!;
        private          string             _lastWebsite  = string.Empty;
        private          bool               _websiteValid;

        private string? _currentSortOrderPath;
        private int?    _currentPriority;

        public ModPanel( ConfigWindow window )
            => _window = window;

        private void Init( ModFileSystemSelector selector )
        {
            _valid = selector.Selected != null;
            if( !_valid )
            {
                return;
            }

            _leaf         = selector.SelectedLeaf!;
            _mod          = selector.Selected!;
            _settings     = selector.SelectedSettings;
            _collection   = selector.SelectedSettingCollection;
            _emptySetting = _settings   == ModSettings2.Empty;
            _inherited    = _collection != Penumbra.CollectionManager.Current;
        }

        public void Draw( ModFileSystemSelector selector )
        {
            Init( selector );
            if( !_valid )
            {
                return;
            }

            DrawInheritedWarning();
            DrawHeaderLine();
            DrawFilesystemPath();
            DrawEnabledInput();
            ImGui.SameLine();
            DrawPriorityInput();
            DrawRemoveSettings();
            DrawTabBar();
        }

        private void DrawDescriptionTab()
        {
            if( _mod.Description.Length == 0 )
            {
                return;
            }

            using var tab = ImRaii.TabItem( "Description" );
            if( !tab )
            {
                return;
            }

            using var child = ImRaii.Child( "##tab" );
            if( !child )
            {
                return;
            }

            ImGui.TextWrapped( _mod.Description );
        }

        private void DrawSettingsTab()
        {
            if( !_mod.HasOptions )
            {
                return;
            }

            using var tab = ImRaii.TabItem( "Settings" );
            if( !tab )
            {
                return;
            }

            using var child = ImRaii.Child( "##tab" );
            if( !child )
            {
                return;
            }

            for( var idx = 0; idx < _mod.Groups.Count; ++idx )
            {
                var group = _mod.Groups[ idx ];
                if( group.Type == SelectType.Single && group.IsOption )
                {
                    using var id             = ImRaii.PushId( idx );
                    var       selectedOption = _emptySetting ? 0 : ( int )_settings.Settings[ idx ];
                    ImGui.SetNextItemWidth( _window._inputTextWidth.X );
                    using var combo = ImRaii.Combo( string.Empty, group[ selectedOption ].Name );
                    if( combo )
                    {
                        for( var idx2 = 0; idx2 < group.Count; ++idx2 )
                        {
                            if( ImGui.Selectable( group[ idx2 ].Name, idx2 == selectedOption ) )
                            {
                                Penumbra.CollectionManager.Current.SetModSetting( _mod.Index, idx, ( uint )idx2 );
                            }
                        }
                    }

                    combo.Dispose();
                    ImGui.SameLine();
                    if( group.Description.Length > 0 )
                    {
                        ImGuiUtil.LabeledHelpMarker( group.Name, group.Description );
                    }
                    else
                    {
                        ImGui.Text( group.Name );
                    }
                }
            }

            // TODO add description
            for( var idx = 0; idx < _mod.Groups.Count; ++idx )
            {
                var group = _mod.Groups[ idx ];
                if( group.Type == SelectType.Multi && group.IsOption )
                {
                    using var id    = ImRaii.PushId( idx );
                    var       flags = _emptySetting ? 0u : _settings.Settings[ idx ];
                    Widget.BeginFramedGroup( group.Name );
                    for( var idx2 = 0; idx2 < group.Count; ++idx2 )
                    {
                        var flag    = 1u << idx2;
                        var setting = ( flags & flag ) != 0;
                        if( ImGui.Checkbox( group[ idx2 ].Name, ref setting ) )
                        {
                            flags = setting ? flags | flag : flags & ~flag;
                            Penumbra.CollectionManager.Current.SetModSetting( _mod.Index, idx, flags );
                        }
                    }

                    Widget.EndFramedGroup();
                }
            }
        }

        private void DrawChangedItemsTab()
        {
            if( _mod.ChangedItems.Count == 0 )
            {
                return;
            }

            using var tab = ImRaii.TabItem( "Changed Items" );
            if( !tab )
            {
                return;
            }

            using var list = ImRaii.ListBox( "##changedItems", -Vector2.One );
            if( !list )
            {
                return;
            }

            foreach( var (name, data) in _mod.ChangedItems )
            {
                _window.DrawChangedItem( name, data );
            }
        }

        private void DrawTabBar()
        {
            using var tabBar = ImRaii.TabBar( "##ModTabs" );
            if( !tabBar )
            {
                return;
            }

            DrawDescriptionTab();
            DrawSettingsTab();
            DrawChangedItemsTab();
        }

        private void DrawInheritedWarning()
        {
            if( _inherited )
            {
                using var color = ImRaii.PushColor( ImGuiCol.Button, Colors.PressEnterWarningBg );
                var       w     = new Vector2( ImGui.GetContentRegionAvail().X, 0 );
                if( ImGui.Button( $"These settings are inherited from {_collection.Name}.", w ) )
                {
                    Penumbra.CollectionManager.Current.SetModInheritance( _mod.Index, false );
                }
            }
        }

        private void DrawPriorityInput()
        {
            var priority = _currentPriority ?? _settings.Priority;
            ImGui.SetNextItemWidth( 50 * ImGuiHelpers.GlobalScale );
            if( ImGui.InputInt( "Priority", ref priority, 0, 0 ) )
            {
                _currentPriority = priority;
            }

            if( ImGui.IsItemDeactivatedAfterEdit() && _currentPriority.HasValue )
            {
                if( _currentPriority != _settings.Priority )
                {
                    Penumbra.CollectionManager.Current.SetModPriority( _mod.Index, _currentPriority.Value );
                }

                _currentPriority = null;
            }
        }

        private void DrawRemoveSettings()
        {
            if( _inherited )
            {
                return;
            }

            ImGui.SameLine();
            if( ImGui.Button( "Remove Settings" ) )
            {
                Penumbra.CollectionManager.Current.SetModInheritance( _mod.Index, true );
            }

            ImGuiUtil.HoverTooltip( "Remove current settings from this collection so that it can inherit them.\n"
              + "If no inherited collection has settings for this mod, it will be disabled." );
        }

        private void DrawEnabledInput()
        {
            var enabled = _settings.Enabled;
            if( ImGui.Checkbox( "Enabled", ref enabled ) )
            {
                Penumbra.CollectionManager.Current.SetModState( _mod.Index, enabled );
            }
        }

        private void DrawFilesystemPath()
        {
            var fullName = _leaf.FullName();
            var path     = _currentSortOrderPath ?? fullName;
            ImGui.SetNextItemWidth( 300 * ImGuiHelpers.GlobalScale );
            if( ImGui.InputText( "Sort Order", ref path, 256 ) )
            {
                _currentSortOrderPath = path;
            }

            if( ImGui.IsItemDeactivatedAfterEdit() && _currentSortOrderPath != null )
            {
                if( _currentSortOrderPath != fullName )
                {
                    _window._penumbra.ModFileSystem.RenameAndMove( _leaf, _currentSortOrderPath );
                }

                _currentSortOrderPath = null;
            }
        }


        // Draw the first info line for the mod panel,
        // containing all basic meta information.
        private void DrawHeaderLine()
        {
            DrawName();
            ImGui.SameLine();
            DrawVersion();
            ImGui.SameLine();
            DrawAuthor();
            ImGui.SameLine();
            DrawWebsite();
        }

        // Draw the mod name.
        private void DrawName()
        {
            ImGui.Text( _mod.Name.Text );
        }

        // Draw the author of the mod, if any.
        private void DrawAuthor()
        {
            using var group = ImRaii.Group();
            ImGuiUtil.TextColored( Colors.MetaInfoText, "by" );
            ImGui.SameLine();
            ImGui.Text( _mod.Author.IsEmpty ? "Unknown" : _mod.Author.Text );
        }

        // Draw the mod version, if any.
        private void DrawVersion()
        {
            if( _mod.Version.Length > 0 )
            {
                ImGui.Text( $"(Version {_mod.Version})" );
            }
            else
            {
                ImGui.Dummy( Vector2.Zero );
            }
        }

        // Update the last seen website and check for validity.
        private void UpdateWebsite( string newWebsite )
        {
            if( _lastWebsite == newWebsite )
            {
                return;
            }

            _lastWebsite = newWebsite;
            _websiteValid = Uri.TryCreate( _lastWebsite, UriKind.Absolute, out var uriResult )
             && ( uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme == Uri.UriSchemeHttp );
        }

        // Draw the website source either as a button to open the site,
        // if it is a valid http website, or as pure text.
        private void DrawWebsite()
        {
            UpdateWebsite( _mod.Website );
            if( _lastWebsite.Length == 0 )
            {
                ImGui.Dummy( Vector2.Zero );
                return;
            }

            using var group = ImRaii.Group();
            if( _websiteValid )
            {
                if( ImGui.Button( "Open Website" ) )
                {
                    try
                    {
                        var process = new ProcessStartInfo( _lastWebsite )
                        {
                            UseShellExecute = true,
                        };
                        Process.Start( process );
                    }
                    catch
                    {
                        // ignored
                    }
                }

                ImGuiUtil.HoverTooltip( _lastWebsite );
            }
            else
            {
                ImGuiUtil.TextColored( Colors.MetaInfoText, "from" );
                ImGui.SameLine();
                ImGui.Text( _lastWebsite );
            }
        }
    }
}

public partial class ConfigWindow
{
    public void DrawModsTab()
    {
        if( !Penumbra.ModManager.Valid )
        {
            return;
        }

        using var tab = ImRaii.TabItem( "Mods" );
        if( !tab )
        {
            return;
        }

        _selector.Draw( GetModSelectorSize() );
        ImGui.SameLine();
        using var group = ImRaii.Group();
        DrawHeaderLine();

        using var child = ImRaii.Child( "##ModsTabMod", -Vector2.One, true );
        if( child )
        {
            _modPanel.Draw( _selector );
        }
    }


    // Draw the header line that can quick switch between collections.
    private void DrawHeaderLine()
    {
        using var style      = ImRaii.PushStyle( ImGuiStyleVar.FrameRounding, 0 ).Push( ImGuiStyleVar.ItemSpacing, Vector2.Zero );
        var       buttonSize = new Vector2( ImGui.GetContentRegionAvail().X / 8f, 0 );

        DrawDefaultCollectionButton( 3 * buttonSize );
        ImGui.SameLine();
        DrawInheritedCollectionButton( 3 * buttonSize );
        ImGui.SameLine();
        DrawCollectionSelector( "##collection", 2 * buttonSize.X, ModCollection.Type.Current, false, null );
    }

    private static void DrawDefaultCollectionButton( Vector2 width )
    {
        var name      = $"Default Collection ({Penumbra.CollectionManager.Default.Name})";
        var isCurrent = Penumbra.CollectionManager.Default == Penumbra.CollectionManager.Current;
        var isEmpty   = Penumbra.CollectionManager.Default == ModCollection.Empty;
        var tt = isCurrent ? "The current collection is already the configured default collection."
            : isEmpty      ? "The default collection is configured to be empty."
                             : "Set the current collection to the configured default collection.";
        if( ImGuiUtil.DrawDisabledButton( name, width, tt, isCurrent || isEmpty ) )
        {
            Penumbra.CollectionManager.SetCollection( Penumbra.CollectionManager.Default, ModCollection.Type.Current );
        }
    }

    private void DrawInheritedCollectionButton( Vector2 width )
    {
        var noModSelected = _selector.Selected == null;
        var collection    = _selector.SelectedSettingCollection;
        var modInherited  = collection != Penumbra.CollectionManager.Current;
        var (name, tt) = ( noModSelected, modInherited ) switch
        {
            (true, _) => ( "Inherited Collection", "No mod selected." ),
            (false, true) => ( $"Inherited Collection ({collection.Name})",
                "Set the current collection to the collection the selected mod inherits its settings from." ),
            (false, false) => ( "Not Inherited", "The selected mod does not inherit its settings." ),
        };
        if( ImGuiUtil.DrawDisabledButton( name, width, tt, noModSelected || !modInherited ) )
        {
            Penumbra.CollectionManager.SetCollection( collection, ModCollection.Type.Current );
        }
    }

    // Get the correct size for the mod selector based on current config.
    private static float GetModSelectorSize()
    {
        var absoluteSize = Math.Clamp( Penumbra.Config.ModSelectorAbsoluteSize, Configuration.Constants.MinAbsoluteSize,
            Math.Min( Configuration.Constants.MaxAbsoluteSize, ImGui.GetContentRegionAvail().X - 100 ) );
        var relativeSize = Penumbra.Config.ScaleModSelector
            ? Math.Clamp( Penumbra.Config.ModSelectorScaledSize, Configuration.Constants.MinScaledSize, Configuration.Constants.MaxScaledSize )
            : 0;
        return !Penumbra.Config.ScaleModSelector
            ? absoluteSize
            : Math.Max( absoluteSize, relativeSize * ImGui.GetContentRegionAvail().X / 100 );
    }
}