using System;
using System.IO;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private partial class SettingsTab
    {
        private static void Checkbox( string label, string tooltip, bool current, Action< bool > setter )
        {
            using var id  = ImRaii.PushId( label );
            var       tmp = current;
            if( ImGui.Checkbox( string.Empty, ref tmp ) && tmp != current )
            {
                setter( tmp );
                Penumbra.Config.Save();
            }

            ImGui.SameLine();
            ImGuiUtil.LabeledHelpMarker( label, tooltip );
        }

        private void DrawModSelectorSettings()
        {
            if( !ImGui.CollapsingHeader( "General" ) )
            {
                OpenTutorial( BasicTutorialSteps.GeneralSettings );
                return;
            }

            OpenTutorial( BasicTutorialSteps.GeneralSettings );

            Checkbox( "Hide Config Window when UI is Hidden",
                "Hide the penumbra main window when you manually hide the in-game user interface.", Penumbra.Config.HideUiWhenUiHidden,
                v =>
                {
                    Penumbra.Config.HideUiWhenUiHidden                  = v;
                    Dalamud.PluginInterface.UiBuilder.DisableUserUiHide = !v;
                } );
            Checkbox( "Hide Config Window when in Cutscenes",
                "Hide the penumbra main window when you are currently watching a cutscene.", Penumbra.Config.HideUiInCutscenes,
                v =>
                {
                    Penumbra.Config.HideUiInCutscenes                       = v;
                    Dalamud.PluginInterface.UiBuilder.DisableCutsceneUiHide = !v;
                } );
            Checkbox( "Hide Config Window when in GPose",
                "Hide the penumbra main window when you are currently in GPose mode.", Penumbra.Config.HideUiInGPose,
                v =>
                {
                    Penumbra.Config.HideUiInGPose                        = v;
                    Dalamud.PluginInterface.UiBuilder.DisableGposeUiHide = !v;
                } );
            ImGui.Dummy( _window._defaultSpace );
            Checkbox( "Use Special Collections in Character Window",
                "Use the character collection for your character's name in your main character window, if it is set.",
                Penumbra.Config.UseCharacterCollectionInMainWindow, v => Penumbra.Config.UseCharacterCollectionInMainWindow = v );
            Checkbox( "Use Special Collections in Adventurer Cards",
                "Use the appropriate character collection for the adventurer card you are currently looking at, based on the adventurer's name.",
                Penumbra.Config.UseCharacterCollectionsInCards, v => Penumbra.Config.UseCharacterCollectionsInCards = v );
            Checkbox( "Use Special Collections in Try-On Window",
                "Use the character collection for your character's name in your try-on, dye preview or glamour plate window, if it is set.",
                Penumbra.Config.UseCharacterCollectionInTryOn, v => Penumbra.Config.UseCharacterCollectionInTryOn = v );
            Checkbox( "Use Special Collections in Inspect Windows",
                "Use the appropriate character collection for the character you are currently inspecting, based on their name.",
                Penumbra.Config.UseCharacterCollectionInInspect, v => Penumbra.Config.UseCharacterCollectionInInspect = v );
            Checkbox( "Use Special Collections based on Ownership",
                "Use the owner's name to determine the appropriate character collection for mounts, companions and combat pets.",
                Penumbra.Config.UseOwnerNameForCharacterCollection, v => Penumbra.Config.UseOwnerNameForCharacterCollection = v );
            Checkbox( "Prefer Named Collections over Ownership",
                "If you have a character collection set to a specific name for a companion or combat pet, prefer this collection over the owner's collection.\n"
              + "That is, if you have a 'Topaz Carbuncle' collection, it will use this one instead of the one for its owner.",
                Penumbra.Config.PreferNamedCollectionsOverOwners, v => Penumbra.Config.PreferNamedCollectionsOverOwners = v );
            Checkbox( "Use Default Collection for Housing Retainers",
                "Housing Retainers use the name of their owner instead of their own, you can decide to let them use their owners character collection or the default collection.\n"
              + "It is not possible to make them have their own collection, since they have no connection to their actual name.",
                Penumbra.Config.UseDefaultCollectionForRetainers, v => Penumbra.Config.UseDefaultCollectionForRetainers = v );
            ImGui.Dummy( _window._defaultSpace );
            DrawFolderSortType();
            DrawAbsoluteSizeSelector();
            DrawRelativeSizeSelector();
            Checkbox( "Open Folders by Default", "Whether to start with all folders collapsed or expanded in the mod selector.",
                Penumbra.Config.OpenFoldersByDefault, v =>
                {
                    Penumbra.Config.OpenFoldersByDefault = v;
                    _window._selector.SetFilterDirty();
                } );

            Widget.DoubleModifierSelector( "Mod Deletion Modifier",
                "A modifier you need to hold while clicking the Delete Mod button for it to take effect.", _window._inputTextWidth.X,
                Penumbra.Config.DeleteModModifier,
                v =>
                {
                    Penumbra.Config.DeleteModModifier = v;
                    Penumbra.Config.Save();
                } );
            ImGui.Dummy( _window._defaultSpace );
            Checkbox( "Always Open Import at Default Directory",
                "Open the import window at the location specified here every time, forgetting your previous path.",
                Penumbra.Config.AlwaysOpenDefaultImport, v => Penumbra.Config.AlwaysOpenDefaultImport = v );
            DrawDefaultModImportPath();
            DrawDefaultModAuthor();
            DrawDefaultModImportFolder();

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
            using var combo = ImRaii.Combo( "##sortMode", sortMode.Name );
            if( combo )
            {
                foreach( var val in Configuration.Constants.ValidSortModes )
                {
                    if( ImGui.Selectable( val.Name, val.GetType() == sortMode.GetType() ) && val.GetType() != sortMode.GetType() )
                    {
                        Penumbra.Config.SortMode = val;
                        _window._selector.SetFilterDirty();
                        Penumbra.Config.Save();
                    }

                    ImGuiUtil.HoverTooltip( val.Description );
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

        private void DrawDefaultModImportFolder()
        {
            var tmp = Penumbra.Config.DefaultImportFolder;
            ImGui.SetNextItemWidth( _window._inputTextWidth.X );
            if( ImGui.InputText( "##defaultImportFolder", ref tmp, 64 ) )
            {
                Penumbra.Config.DefaultImportFolder = tmp;
            }

            if( ImGui.IsItemDeactivatedAfterEdit() )
            {
                Penumbra.Config.Save();
            }

            ImGuiUtil.LabeledHelpMarker( "Default Mod Import Folder",
                "Set the default Penumbra mod folder to place newly imported mods into.\nLeave blank to import into Root." );
        }
    }
}