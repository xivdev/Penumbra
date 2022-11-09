using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private partial class ModPanel
    {
        private ModSettings                 _settings   = null!;
        private ModCollection               _collection = null!;
        private bool                        _emptySetting;
        private bool                        _inherited;
        private SingleArray< ModConflicts > _conflicts = new();

        private int? _currentPriority;

        private void UpdateSettingsData( ModFileSystemSelector selector )
        {
            _settings     = selector.SelectedSettings;
            _collection   = selector.SelectedSettingCollection;
            _emptySetting = _settings   == ModSettings.Empty;
            _inherited    = _collection != Penumbra.CollectionManager.Current;
            _conflicts    = Penumbra.CollectionManager.Current.Conflicts( _mod );
        }

        // Draw the whole settings tab as well as its contents.
        private void DrawSettingsTab()
        {
            using var tab = DrawTab( SettingsTabHeader, Tabs.Settings );
            OpenTutorial( BasicTutorialSteps.ModOptions );
            if( !tab )
            {
                return;
            }

            using var child = ImRaii.Child( "##settings" );
            if( !child )
            {
                return;
            }

            DrawInheritedWarning();
            ImGui.Dummy( _window._defaultSpace );
            _window._penumbra.Api.InvokePreSettingsPanel( _mod.ModPath.Name );
            DrawEnabledInput();
            OpenTutorial( BasicTutorialSteps.EnablingMods );
            ImGui.SameLine();
            DrawPriorityInput();
            OpenTutorial( BasicTutorialSteps.Priority );
            DrawRemoveSettings();

            if( _mod.Groups.Count > 0 )
            {
                var useDummy = true;
                foreach(var (group, idx) in _mod.Groups.WithIndex().Where(g => g.Value.Type == GroupType.Single && g.Value.IsOption  ))
                {
                    ImGuiUtil.Dummy( _window._defaultSpace, useDummy );
                    useDummy = false;
                    DrawSingleGroup( group, idx );
                }

                useDummy = true;
                foreach( var (group, idx) in _mod.Groups.WithIndex().Where( g => g.Value.Type == GroupType.Multi && g.Value.IsOption ) )
                {
                    ImGuiUtil.Dummy( _window._defaultSpace, useDummy );
                    useDummy = false;
                    DrawMultiGroup( group, idx );
                }
            }

            ImGui.Dummy( _window._defaultSpace );
            _window._penumbra.Api.InvokePostSettingsPanel( _mod.ModPath.Name );
        }


        // Draw a big red bar if the current setting is inherited.
        private void DrawInheritedWarning()
        {
            if( !_inherited )
            {
                return;
            }

            using var color = ImRaii.PushColor( ImGuiCol.Button, Colors.PressEnterWarningBg );
            var       width = new Vector2( ImGui.GetContentRegionAvail().X, 0 );
            if( ImGui.Button( $"These settings are inherited from {_collection.Name}.", width ) )
            {
                Penumbra.CollectionManager.Current.SetModInheritance( _mod.Index, false );
            }

            ImGuiUtil.HoverTooltip( "You can click this button to copy the current settings to the current selection.\n"
              + "You can also just change any setting, which will copy the settings with the single setting changed to the current selection." );
        }

        // Draw a checkbox for the enabled status of the mod.
        private void DrawEnabledInput()
        {
            var enabled = _settings.Enabled;
            if( ImGui.Checkbox( "Enabled", ref enabled ) )
            {
                Penumbra.ModManager.NewMods.Remove( _mod );
                Penumbra.CollectionManager.Current.SetModState( _mod.Index, enabled );
            }
        }

        // Draw a priority input.
        // Priority is changed on deactivation of the input box.
        private void DrawPriorityInput()
        {
            using var group    = ImRaii.Group();
            var       priority = _currentPriority ?? _settings.Priority;
            ImGui.SetNextItemWidth( 50 * ImGuiHelpers.GlobalScale );
            if( ImGui.InputInt( "##Priority", ref priority, 0, 0 ) )
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

            ImGuiUtil.LabeledHelpMarker( "Priority", "Mods with a higher number here take precedence before Mods with a lower number.\n"
              + "That means, if Mod A should overwrite changes from Mod B, Mod A should have a higher priority number than Mod B." );
        }

        // Draw a button to remove the current settings and inherit them instead
        // on the top-right corner of the window/tab.
        private void DrawRemoveSettings()
        {
            const string text = "Inherit Settings";
            if( _inherited || _emptySetting )
            {
                return;
            }

            var scroll = ImGui.GetScrollMaxY() > 0 ? ImGui.GetStyle().ScrollbarSize : 0;
            ImGui.SameLine( ImGui.GetWindowWidth() - ImGui.CalcTextSize( text ).X - ImGui.GetStyle().FramePadding.X * 2 - scroll );
            if( ImGui.Button( text ) )
            {
                Penumbra.CollectionManager.Current.SetModInheritance( _mod.Index, true );
            }

            ImGuiUtil.HoverTooltip( "Remove current settings from this collection so that it can inherit them.\n"
              + "If no inherited collection has settings for this mod, it will be disabled." );
        }

        // Draw a single group selector as a combo box.
        // If a description is provided, add a help marker besides it.
        private void DrawSingleGroup( IModGroup group, int groupIdx )
        {
            using var id             = ImRaii.PushId( groupIdx );
            var       selectedOption = _emptySetting ? ( int )group.DefaultSettings : ( int )_settings.Settings[ groupIdx ];
            ImGui.SetNextItemWidth( _window._inputTextWidth.X * 3 / 4 );
            using var combo = ImRaii.Combo( string.Empty, group[ selectedOption ].Name );
            if( combo )
            {
                for( var idx2 = 0; idx2 < group.Count; ++idx2 )
                {
                    id.Push( idx2 );
                    if( ImGui.Selectable( group[ idx2 ].Name, idx2 == selectedOption ) )
                    {
                        Penumbra.CollectionManager.Current.SetModSetting( _mod.Index, groupIdx, ( uint )idx2 );
                    }

                    id.Pop();
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
                ImGui.TextUnformatted( group.Name );
            }
        }

        // Draw a multi group selector as a bordered set of checkboxes.
        // If a description is provided, add a help marker in the title.
        private void DrawMultiGroup( IModGroup group, int groupIdx )
        {
            using var id    = ImRaii.PushId( groupIdx );
            var       flags = _emptySetting ? group.DefaultSettings : _settings.Settings[ groupIdx ];
            Widget.BeginFramedGroup( group.Name, group.Description );
            for( var idx2 = 0; idx2 < group.Count; ++idx2 )
            {
                id.Push( idx2 );
                var flag    = 1u << idx2;
                var setting = ( flags & flag ) != 0;
                if( ImGui.Checkbox( group[ idx2 ].Name, ref setting ) )
                {
                    flags = setting ? flags | flag : flags & ~flag;
                    Penumbra.CollectionManager.Current.SetModSetting( _mod.Index, groupIdx, flags );
                }

                id.Pop();
            }

            Widget.EndFramedGroup();
            var label = $"##multi{groupIdx}";
            if( ImGui.IsItemClicked( ImGuiMouseButton.Right ) )
            {
                ImGui.OpenPopup( $"##multi{groupIdx}" );
            }

            using var style = ImRaii.PushStyle( ImGuiStyleVar.PopupBorderSize, 1 );
            using var popup = ImRaii.Popup( label );
            if( popup )
            {
                ImGui.TextUnformatted( group.Name );
                ImGui.Separator();
                if( ImGui.Selectable( "Enable All" ) )
                {
                    flags = group.Count == 32 ? uint.MaxValue : ( 1u << group.Count ) - 1u;
                    Penumbra.CollectionManager.Current.SetModSetting( _mod.Index, groupIdx, flags );
                }

                if( ImGui.Selectable( "Disable All" ) )
                {
                    Penumbra.CollectionManager.Current.SetModSetting( _mod.Index, groupIdx, 0 );
                }
            }
        }
    }
}