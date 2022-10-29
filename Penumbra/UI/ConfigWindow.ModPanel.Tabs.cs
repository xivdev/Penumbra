using System;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private partial class ModPanel
    {
        [Flags]
        private enum Tabs
        {
            Description  = 0x01,
            Settings     = 0x02,
            ChangedItems = 0x04,
            Conflicts    = 0x08,
            Edit         = 0x10,
        };

        // We want to keep the preferred tab selected even if switching through mods.
        private Tabs _preferredTab  = Tabs.Settings;
        private Tabs _availableTabs = 0;

        // Required to use tabs that can not be closed but have a flag to set them open.
        private static readonly ByteString ConflictTabHeader     = ByteString.FromSpanUnsafe( "Conflicts"u8, true, false, true );
        private static readonly ByteString DescriptionTabHeader  = ByteString.FromSpanUnsafe( "Description"u8, true, false, true );
        private static readonly ByteString SettingsTabHeader     = ByteString.FromSpanUnsafe( "Settings"u8, true, false, true );
        private static readonly ByteString ChangedItemsTabHeader = ByteString.FromSpanUnsafe( "Changed Items"u8, true, false, true );
        private static readonly ByteString EditModTabHeader      = ByteString.FromSpanUnsafe( "Edit Mod"u8, true, false, true );

        private readonly TagButtons _modTags = new();

        private void DrawTabBar()
        {
            var       tabBarHeight = ImGui.GetCursorPosY();
            using var tabBar       = ImRaii.TabBar( "##ModTabs" );
            if( !tabBar )
            {
                return;
            }

            _availableTabs = Tabs.Settings
              | ( _mod.ChangedItems.Count > 0 ? Tabs.ChangedItems : 0 )
              | Tabs.Description
              | ( _conflicts.Count > 0 ? Tabs.Conflicts : 0 )
              | Tabs.Edit;

            DrawSettingsTab();
            DrawDescriptionTab();
            DrawChangedItemsTab();
            DrawConflictsTab();
            DrawEditModTab();
            DrawAdvancedEditingButton();
            DrawFavoriteButton( tabBarHeight );
        }

        private void DrawAdvancedEditingButton()
        {
            if( ImGui.TabItemButton( "Advanced Editing", ImGuiTabItemFlags.Trailing | ImGuiTabItemFlags.NoTooltip ) )
            {
                _window.ModEditPopup.ChangeMod( _mod );
                _window.ModEditPopup.ChangeOption( _mod.Default );
                _window.ModEditPopup.IsOpen = true;
            }

            ImGuiUtil.HoverTooltip(
                "Clicking this will open a new window in which you can\nedit the following things per option for this mod:\n\n"
              + "\t\t- file redirections\n"
              + "\t\t- file swaps\n"
              + "\t\t- metadata manipulations\n"
              + "\t\t- model materials\n"
              + "\t\t- duplicates\n"
              + "\t\t- textures" );
        }

        private void DrawFavoriteButton( float height )
        {
            var oldPos = ImGui.GetCursorPos();

            using( var font = ImRaii.PushFont( UiBuilder.IconFont ) )
            {
                var size   = ImGui.CalcTextSize( FontAwesomeIcon.Star.ToIconString() ) + ImGui.GetStyle().FramePadding * 2;
                var newPos = new Vector2( ImGui.GetWindowWidth() - size.X - ImGui.GetStyle().ItemSpacing.X, height );
                if( ImGui.GetScrollMaxX() > 0 )
                {
                    newPos.X += ImGui.GetScrollX();
                }

                var rectUpper = ImGui.GetWindowPos() + newPos;
                var color = ImGui.IsMouseHoveringRect( rectUpper, rectUpper + size ) ? ImGui.GetColorU32( ImGuiCol.Text ) :
                    _mod.Favorite                                                    ? 0xFF00FFFF : ImGui.GetColorU32( ImGuiCol.TextDisabled );
                using var c = ImRaii.PushColor( ImGuiCol.Text, color )
                   .Push( ImGuiCol.Button, 0 )
                   .Push( ImGuiCol.ButtonHovered, 0 )
                   .Push( ImGuiCol.ButtonActive, 0 );

                ImGui.SetCursorPos( newPos );
                if( ImGui.Button( FontAwesomeIcon.Star.ToIconString() ) )
                {
                    Penumbra.ModManager.ChangeModFavorite( _mod.Index, !_mod.Favorite );
                }
            }

            var hovered = ImGui.IsItemHovered();
            OpenTutorial( BasicTutorialSteps.Favorites );

            if( hovered )
            {
                ImGui.SetTooltip( "Favorite" );
            }
        }


        // Just a simple text box with the wrapped description, if it exists.
        private void DrawDescriptionTab()
        {
            using var tab = DrawTab( DescriptionTabHeader, Tabs.Description );
            if( !tab )
            {
                return;
            }

            using var child = ImRaii.Child( "##description" );
            if( !child )
            {
                return;
            }

            ImGui.Dummy( ImGuiHelpers.ScaledVector2( 2 ) );

            ImGui.Dummy( ImGuiHelpers.ScaledVector2( 2 ) );
            var tagIdx = _localTags.Draw( "Local Tags: ", "Custom tags you can set personally that will not be exported to the mod data but only set for you.\n"
              + "If the mod already contains a local tag in its own tags, the local tag will be ignored.", _mod.LocalTags,
                out var editedTag );
            OpenTutorial( BasicTutorialSteps.Tags );
            if( tagIdx >= 0 )
            {
                Penumbra.ModManager.ChangeLocalTag( _mod.Index, tagIdx, editedTag );
            }

            if( _mod.ModTags.Count > 0 )
            {
                _modTags.Draw( "Mod Tags: ", "Tags assigned by the mod creator and saved with the mod data. To edit these, look at Edit Mod.", _mod.ModTags, out var _, false,
                    ImGui.CalcTextSize( "Local " ).X - ImGui.CalcTextSize( "Mod " ).X );
            }

            ImGui.Dummy( ImGuiHelpers.ScaledVector2( 2 ) );
            ImGui.Separator();

            ImGuiUtil.TextWrapped( _mod.Description );
        }

        // A simple clipped list of changed items.
        private void DrawChangedItemsTab()
        {
            using var tab = DrawTab( ChangedItemsTabHeader, Tabs.ChangedItems );
            if( !tab )
            {
                return;
            }

            using var list = ImRaii.ListBox( "##changedItems", -Vector2.One );
            if( !list )
            {
                return;
            }

            var zipList = ZipList.FromSortedList( _mod.ChangedItems );
            var height  = ImGui.GetTextLineHeight();
            ImGuiClip.ClippedDraw( zipList, kvp => _window.DrawChangedItem( kvp.Item1, kvp.Item2, true ), height );
        }

        // If any conflicts exist, show them in this tab.
        private unsafe void DrawConflictsTab()
        {
            using var tab = DrawTab( ConflictTabHeader, Tabs.Conflicts );
            if( !tab )
            {
                return;
            }

            using var box = ImRaii.ListBox( "##conflicts", -Vector2.One );
            if( !box )
            {
                return;
            }

            foreach( var conflict in Penumbra.CollectionManager.Current.Conflicts( _mod ) )
            {
                if( ImGui.Selectable( conflict.Mod2.Name ) && conflict.Mod2 is Mod mod )
                {
                    _window._selector.SelectByValue( mod );
                }

                ImGui.SameLine();
                using( var color = ImRaii.PushColor( ImGuiCol.Text,
                          conflict.HasPriority ? ColorId.HandledConflictMod.Value() : ColorId.ConflictingMod.Value() ) )
                {
                    var priority = conflict.Mod2.Index < 0
                        ? conflict.Mod2.Priority
                        : Penumbra.CollectionManager.Current[ conflict.Mod2.Index ].Settings!.Priority;
                    ImGui.TextUnformatted( $"(Priority {priority})" );
                }

                using var indent = ImRaii.PushIndent( 30f );
                foreach( var data in conflict.Conflicts )
                {
                    var _ = data switch
                    {
                        Utf8GamePath p     => ImGuiNative.igSelectable_Bool( p.Path.Path, 0, ImGuiSelectableFlags.None, Vector2.Zero ) > 0,
                        MetaManipulation m => ImGui.Selectable( m.Manipulation?.ToString() ?? string.Empty ),
                        _                  => false,
                    };
                }
            }
        }


        // Draw a tab by given name if it is available, and deal with changing the preferred tab.
        private ImRaii.IEndObject DrawTab( ByteString name, Tabs flag )
        {
            if( !_availableTabs.HasFlag( flag ) )
            {
                return ImRaii.IEndObject.Empty;
            }

            var flags = _preferredTab == flag ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
            unsafe
            {
                var tab = ImRaii.TabItem( name.Path, flags );
                if( ImGui.IsItemClicked() )
                {
                    _preferredTab = flag;
                }

                return tab;
            }
        }
    }
}