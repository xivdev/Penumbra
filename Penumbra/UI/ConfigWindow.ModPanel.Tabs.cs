using System;
using System.Numerics;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
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
        private static readonly Utf8String ConflictTabHeader     = Utf8String.FromStringUnsafe( "Conflicts", false );
        private static readonly Utf8String DescriptionTabHeader  = Utf8String.FromStringUnsafe( "Description", false );
        private static readonly Utf8String SettingsTabHeader     = Utf8String.FromStringUnsafe( "Settings", false );
        private static readonly Utf8String ChangedItemsTabHeader = Utf8String.FromStringUnsafe( "Changed Items", false );
        private static readonly Utf8String EditModTabHeader      = Utf8String.FromStringUnsafe( "Edit Mod", false );

        private void DrawTabBar()
        {
            ImGui.Dummy( _window._defaultSpace );
            using var tabBar = ImRaii.TabBar( "##ModTabs" );
            if( !tabBar )
            {
                return;
            }

            _availableTabs = Tabs.Settings
              | ( _mod.ChangedItems.Count > 0 ? Tabs.ChangedItems : 0 )
              | ( _mod.Description.Length > 0 ? Tabs.Description : 0 )
              | ( _conflicts.Count        > 0 ? Tabs.Conflicts : 0 )
              | ( Penumbra.Config.ShowAdvanced ? Tabs.Edit : 0 );

            DrawSettingsTab();
            DrawDescriptionTab();
            DrawChangedItemsTab();
            DrawConflictsTab();
            DrawEditModTab();
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

            ImGui.TextWrapped( _mod.Description );
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
            ImGuiClip.ClippedDraw( zipList, kvp => _window.DrawChangedItem( kvp.Item1, kvp.Item2 ), height );
        }

        // If any conflicts exist, show them in this tab.
        private void DrawConflictsTab()
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

            var       conflicts = Penumbra.CollectionManager.Current.ModConflicts( _mod.Index );
            Mod?      oldBadMod = null;
            using var indent    = ImRaii.PushIndent( 0f );
            foreach( var conflict in conflicts )
            {
                var badMod = Penumbra.ModManager[ conflict.Mod2 ];
                if( badMod != oldBadMod )
                {
                    if( oldBadMod != null )
                    {
                        indent.Pop( 30f );
                    }

                    if( ImGui.Selectable( badMod.Name ) )
                    {
                        _window._selector.SelectByValue( badMod );
                    }

                    ImGui.SameLine();
                    using var color = ImRaii.PushColor( ImGuiCol.Text,
                        conflict.Mod1Priority ? ColorId.HandledConflictMod.Value() : ColorId.ConflictingMod.Value() );
                    ImGui.TextUnformatted( $"(Priority {Penumbra.CollectionManager.Current[ conflict.Mod2 ].Settings!.Priority})" );

                    indent.Push( 30f );
                }

                if( conflict.Data is Utf8GamePath p )
                {
                    unsafe
                    {
                        ImGuiNative.igSelectable_Bool( p.Path.Path, 0, ImGuiSelectableFlags.None, Vector2.Zero );
                    }
                }
                else if( conflict.Data is MetaManipulation m )
                {
                    ImGui.Selectable( m.Manipulation?.ToString() ?? string.Empty );
                }

                oldBadMod = badMod;
            }
        }


        // Draw a tab by given name if it is available, and deal with changing the preferred tab.
        private ImRaii.IEndObject DrawTab( Utf8String name, Tabs flag )
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