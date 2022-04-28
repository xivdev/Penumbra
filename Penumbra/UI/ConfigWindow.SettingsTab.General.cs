using System;
using System.IO;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Filesystem;
using OtterGui.Raii;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private partial class SettingsTab
    {
        private void DrawModSelectorSettings()
        {
            if( !ImGui.CollapsingHeader( "General" ) )
            {
                return;
            }

            DrawFolderSortType();
            DrawAbsoluteSizeSelector();
            DrawRelativeSizeSelector();
            ImGui.Dummy( _window._defaultSpace );
            DrawDefaultModImportPath();
            DrawDefaultModAuthor();

            ImGui.NewLine();
        }

        // Store separately to use IsItemDeactivatedAfterEdit.
        private float _absoluteSelectorSize = Penumbra.Config.ModSelectorAbsoluteSize;
        private int   _relativeSelectorSize = Penumbra.Config.ModSelectorScaledSize;

        // Different supported sort modes as a combo.
        private void DrawFolderSortType()
        {
            var sortMode = Penumbra.Config.SortMode;
            ImGui.SetNextItemWidth( _window._inputTextWidth.X );
            using var combo = ImRaii.Combo( "##sortMode", sortMode.Data().Name );
            if( combo )
            {
                foreach( var val in Enum.GetValues< SortMode >() )
                {
                    var (name, desc) = val.Data();
                    if( ImGui.Selectable( name, val == sortMode ) && val != sortMode )
                    {
                        Penumbra.Config.SortMode = val;
                        _window._selector.SetFilterDirty();
                        Penumbra.Config.Save();
                    }

                    ImGuiUtil.HoverTooltip( desc );
                }
            }

            combo.Dispose();
            ImGuiUtil.LabeledHelpMarker( "Sort Mode", "Choose the sort mode for the mod selector in the mods tab." );
        }

        // Absolute size in pixels.
        private void DrawAbsoluteSizeSelector()
        {
            if( ImGuiUtil.DragFloat( "##absoluteSize", ref _absoluteSelectorSize, _window._inputTextWidth.X, 1,
                   Configuration.Constants.MinAbsoluteSize, Configuration.Constants.MaxAbsoluteSize, "%.0f" )
            && _absoluteSelectorSize != Penumbra.Config.ModSelectorAbsoluteSize )
            {
                Penumbra.Config.ModSelectorAbsoluteSize = _absoluteSelectorSize;
                Penumbra.Config.Save();
            }

            ImGui.SameLine();
            ImGuiUtil.LabeledHelpMarker( "Mod Selector Absolute Size",
                "The minimal absolute size of the mod selector in the mod tab in pixels." );
        }

        // Relative size toggle and percentage.
        private void DrawRelativeSizeSelector()
        {
            var scaleModSelector = Penumbra.Config.ScaleModSelector;
            if( ImGui.Checkbox( "Scale Mod Selector With Window Size", ref scaleModSelector ) )
            {
                Penumbra.Config.ScaleModSelector = scaleModSelector;
                Penumbra.Config.Save();
            }

            ImGui.SameLine();
            if( ImGuiUtil.DragInt( "##relativeSize", ref _relativeSelectorSize, _window._inputTextWidth.X - ImGui.GetCursorPosX(), 0.1f,
                   Configuration.Constants.MinScaledSize, Configuration.Constants.MaxScaledSize, "%i%%" )
            && _relativeSelectorSize != Penumbra.Config.ModSelectorScaledSize )
            {
                Penumbra.Config.ModSelectorScaledSize = _relativeSelectorSize;
                Penumbra.Config.Save();
            }

            ImGui.SameLine();
            ImGuiUtil.LabeledHelpMarker( "Mod Selector Relative Size",
                "Instead of keeping the mod-selector in the Installed Mods tab a fixed width, this will let it scale with the total size of the Penumbra window." );
        }

        private void DrawDefaultModImportPath()
        {
            var       tmp     = Penumbra.Config.DefaultModImportPath;
            var       spacing = new Vector2( 3 * ImGuiHelpers.GlobalScale );
            using var style   = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, spacing );
            ImGui.SetNextItemWidth( _window._inputTextWidth.X - _window._iconButtonSize.X - spacing.X );
            if( ImGui.InputText( "##defaultModImport", ref tmp, 256 ) )
            {
                Penumbra.Config.DefaultModImportPath = tmp;
            }

            if( ImGui.IsItemDeactivatedAfterEdit() )
            {
                Penumbra.Config.Save();
            }

            ImGui.SameLine();
            if( ImGuiUtil.DrawDisabledButton( $"{FontAwesomeIcon.Folder.ToIconString()}##import", _window._iconButtonSize,
                   "Select a directory via dialog.", false, true ) )
            {
                if( _dialogOpen )
                {
                    _dialogManager.Reset();
                    _dialogOpen = false;
                }
                else
                {
                    var startDir = Directory.Exists( Penumbra.Config.ModDirectory ) ? Penumbra.Config.ModDirectory : ".";

                    _dialogManager.OpenFolderDialog( "Choose Default Import Directory", ( b, s ) =>
                    {
                        Penumbra.Config.DefaultModImportPath = b ? s : Penumbra.Config.DefaultModImportPath;
                        Penumbra.Config.Save();
                        _dialogOpen = false;
                    }, startDir );
                    _dialogOpen = true;
                }
            }

            style.Pop();
            ImGuiUtil.LabeledHelpMarker( "Default Mod Import Directory",
                "Set the directory that gets opened when using the file picker to import mods for the first time." );
        }

        private void DrawDefaultModAuthor()
        {
            var tmp = Penumbra.Config.DefaultModAuthor;
            ImGui.SetNextItemWidth( _window._inputTextWidth.X );
            if( ImGui.InputText( "##defaultAuthor", ref tmp, 64 ) )
            {
                Penumbra.Config.DefaultModAuthor = tmp;
            }

            if( ImGui.IsItemDeactivatedAfterEdit() )
            {
                Penumbra.Config.Save();
            }

            ImGuiUtil.LabeledHelpMarker( "Default Mod Author", "Set the default author stored for newly created mods." );
        }
    }
}