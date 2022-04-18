using System;
using ImGuiNET;
using OtterGui;
using OtterGui.Filesystem;
using OtterGui.Raii;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    // Store separately to use IsItemDeactivatedAfterEdit.
    private float _absoluteSelectorSize = Penumbra.Config.ModSelectorAbsoluteSize;
    private int   _relativeSelectorSize = Penumbra.Config.ModSelectorScaledSize;

    // Different supported sort modes as a combo.
    private void DrawFolderSortType()
    {
        var sortMode = Penumbra.Config.SortMode;
        ImGui.SetNextItemWidth( _inputTextWidth.X );
        using var combo = ImRaii.Combo( "##sortMode", sortMode.Data().Name );
        if( combo )
        {
            foreach( var val in Enum.GetValues< SortMode >() )
            {
                var (name, desc) = val.Data();
                if( ImGui.Selectable( name, val == sortMode ) && val != sortMode )
                {
                    Penumbra.Config.SortMode = val;
                    Selector.SetFilterDirty();
                    Penumbra.Config.Save();
                }

                ImGuiUtil.HoverTooltip( desc );
            }
        }

        combo.Dispose();
        ImGuiUtil.LabeledHelpMarker( "Sort Mode", "Choose the sort mode for the mod selector in the mods tab." );
    }

    private void DrawAbsoluteSizeSelector()
    {
        if( ImGuiUtil.DragFloat( "##absoluteSize", ref _absoluteSelectorSize, _inputTextWidth.X, 1,
               Configuration.Constants.MinAbsoluteSize, Configuration.Constants.MaxAbsoluteSize, "%.0f" )
        && _absoluteSelectorSize != Penumbra.Config.ModSelectorAbsoluteSize )
        {
            Penumbra.Config.ModSelectorAbsoluteSize = _absoluteSelectorSize;
            Penumbra.Config.Save();
        }

        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker( "Mod Selector Absolute Size", "The minimal absolute size of the mod selector in the mod tab in pixels." );
    }

    private void DrawRelativeSizeSelector()
    {
        var scaleModSelector = Penumbra.Config.ScaleModSelector;
        if( ImGui.Checkbox( "Scale Mod Selector With Window Size", ref scaleModSelector ) )
        {
            Penumbra.Config.ScaleModSelector = scaleModSelector;
            Penumbra.Config.Save();
        }

        ImGui.SameLine();
        if( ImGuiUtil.DragInt( "##relativeSize", ref _relativeSelectorSize, _inputTextWidth.X - ImGui.GetCursorPosX(), 0.1f,
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

    private void DrawModSelectorSettings()
    {
        if( !ImGui.CollapsingHeader( "Mod Selector" ) )
        {
            return;
        }

        DrawFolderSortType();
        DrawAbsoluteSizeSelector();
        DrawRelativeSizeSelector();

        ImGui.NewLine();
    }
}