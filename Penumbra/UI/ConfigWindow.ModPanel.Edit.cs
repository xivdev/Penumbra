using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Logging;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Mods;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private partial class ModPanel
    {
        private Vector2 _cellPadding = Vector2.Zero;
        private Vector2 _itemSpacing = Vector2.Zero;

        // Draw the edit tab that contains all things concerning editing the mod.
        private void DrawEditModTab()
        {
            using var tab = DrawTab( EditModTabHeader, Tabs.Edit );
            if( !tab )
            {
                return;
            }

            using var child = ImRaii.Child( "##editChild", -Vector2.One );
            if( !child )
            {
                return;
            }

            _cellPadding = ImGui.GetStyle().CellPadding with { X = 2 * ImGuiHelpers.GlobalScale };
            _itemSpacing = ImGui.GetStyle().CellPadding with { X = 4 * ImGuiHelpers.GlobalScale };

            EditButtons();
            EditRegularMeta();
            ImGui.Dummy( _window._defaultSpace );

            if( Input.Text( "Mod Path", Input.Path, Input.None, _leaf.FullName(), out var newPath, 256,
                   _window._inputTextWidth.X ) )
            {
                try
                {
                    _window._penumbra.ModFileSystem.RenameAndMove( _leaf, newPath );
                }
                catch( Exception e )
                {
                    PluginLog.Warning( e.Message );
                }
            }

            ImGui.Dummy( _window._defaultSpace );
            AddOptionGroup.Draw( _window, _mod );
            ImGui.Dummy( _window._defaultSpace );

            for( var groupIdx = 0; groupIdx < _mod.Groups.Count; ++groupIdx )
            {
                EditGroup( groupIdx );
            }

            EndActions();
            DescriptionEdit.DrawPopup( _window );
        }

        // The general edit row for non-detailed mod edits.
        private void EditButtons()
        {
            var buttonSize   = new Vector2( 150 * ImGuiHelpers.GlobalScale, 0 );
            var folderExists = Directory.Exists( _mod.ModPath.FullName );
            var tt = folderExists
                ? $"Open \"{_mod.ModPath.FullName}\" in the file explorer of your choice."
                : $"Mod directory \"{_mod.ModPath.FullName}\" does not exist.";
            if( ImGuiUtil.DrawDisabledButton( "Open Mod Directory", buttonSize, tt, !folderExists ) )
            {
                Process.Start( new ProcessStartInfo( _mod.ModPath.FullName ) { UseShellExecute = true } );
            }

            ImGui.SameLine();
            if( ImGuiUtil.DrawDisabledButton( "Reload Mod", buttonSize, "Reload the current mod from its files.\n"
                 + "If the mod directory or meta file do not exist anymore or if the new mod name is empty, the mod is deleted instead.",
                   false ) )
            {
                Penumbra.ModManager.ReloadMod( _mod.Index );
            }

            BackupButtons( buttonSize );
            MoveDirectory.Draw( _mod, buttonSize );

            ImGui.Dummy( _window._defaultSpace );
        }

        private void BackupButtons( Vector2 buttonSize )
        {
            var backup = new ModBackup( _mod );
            var tt = ModBackup.CreatingBackup
                ? "Already creating a backup."
                : backup.Exists
                    ? $"Overwrite current backup \"{backup.Name}\" with current mod."
                    : $"Create backup archive of current mod at \"{backup.Name}\".";
            if( ImGuiUtil.DrawDisabledButton( "Create Backup", buttonSize, tt, ModBackup.CreatingBackup ) )
            {
                backup.CreateAsync();
            }

            ImGui.SameLine();
            tt = backup.Exists
                ? $"Delete existing backup file \"{backup.Name}\"."
                : $"Backup file \"{backup.Name}\" does not exist.";
            if( ImGuiUtil.DrawDisabledButton( "Delete Backup", buttonSize, tt, !backup.Exists ) )
            {
                backup.Delete();
            }

            tt = backup.Exists
                ? $"Restore mod from backup file \"{backup.Name}\"."
                : $"Backup file \"{backup.Name}\" does not exist.";
            ImGui.SameLine();
            if( ImGuiUtil.DrawDisabledButton( "Restore From Backup", buttonSize, tt, !backup.Exists ) )
            {
                backup.Restore();
            }
        }

        // Anything about editing the regular meta information about the mod.
        private void EditRegularMeta()
        {
            if( Input.Text( "Name", Input.Name, Input.None, _mod.Name, out var newName, 256, _window._inputTextWidth.X ) )
            {
                Penumbra.ModManager.ChangeModName( _mod.Index, newName );
            }

            if( Input.Text( "Author", Input.Author, Input.None, _mod.Author, out var newAuthor, 256, _window._inputTextWidth.X ) )
            {
                Penumbra.ModManager.ChangeModAuthor( _mod.Index, newAuthor );
            }

            if( Input.Text( "Version", Input.Version, Input.None, _mod.Version, out var newVersion, 32,
                   _window._inputTextWidth.X ) )
            {
                Penumbra.ModManager.ChangeModVersion( _mod.Index, newVersion );
            }

            if( Input.Text( "Website", Input.Website, Input.None, _mod.Website, out var newWebsite, 256,
                   _window._inputTextWidth.X ) )
            {
                Penumbra.ModManager.ChangeModWebsite( _mod.Index, newWebsite );
            }

            var       spacing = new Vector2( 3 * ImGuiHelpers.GlobalScale );
            using var style   = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, spacing );

            var reducedSize = new Vector2( _window._inputTextWidth.X - _window._iconButtonSize.X - spacing.X, 0 );
            if( ImGui.Button( "Edit Description", reducedSize ) )
            {
                _delayedActions.Enqueue( () => DescriptionEdit.OpenPopup( _mod, Input.Description ) );
            }

            ImGui.SameLine();
            var fileExists = File.Exists( _mod.MetaFile.FullName );
            var tt = fileExists
                ? "Open the metadata json file in the text editor of your choice."
                : "The metadata json file does not exist.";
            if( ImGuiUtil.DrawDisabledButton( $"{FontAwesomeIcon.FileExport.ToIconString()}##metaFile", _window._iconButtonSize, tt,
                   !fileExists, true ) )
            {
                Process.Start( new ProcessStartInfo( _mod.MetaFile.FullName ) { UseShellExecute = true } );
            }
        }

        // Do some edits outside of iterations.
        private readonly Queue< Action > _delayedActions = new();

        // Delete a marked group or option outside of iteration.
        private void EndActions()
        {
            while( _delayedActions.TryDequeue( out var action ) )
            {
                action.Invoke();
            }
        }

        // Text input to add a new option group at the end of the current groups.
        private static class AddOptionGroup
        {
            private static string _newGroupName = string.Empty;

            public static void Reset()
                => _newGroupName = string.Empty;

            public static void Draw( ConfigWindow window, Mod mod )
            {
                using var spacing = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, new Vector2( 3 * ImGuiHelpers.GlobalScale ) );
                ImGui.SetNextItemWidth( window._inputTextWidth.X - window._iconButtonSize.X - 3 * ImGuiHelpers.GlobalScale );
                ImGui.InputTextWithHint( "##newGroup", "Add new option group...", ref _newGroupName, 256 );
                ImGui.SameLine();
                var fileExists = File.Exists( mod.DefaultFile );
                var tt = fileExists
                    ? "Open the default option json file in the text editor of your choice."
                    : "The default option json file does not exist.";
                if( ImGuiUtil.DrawDisabledButton( $"{FontAwesomeIcon.FileExport.ToIconString()}##defaultFile", window._iconButtonSize, tt,
                       !fileExists, true ) )
                {
                    Process.Start( new ProcessStartInfo( mod.DefaultFile ) { UseShellExecute = true } );
                }

                ImGui.SameLine();

                var nameValid = Mod.Manager.VerifyFileName( mod, null, _newGroupName, false );
                tt = nameValid ? "Add new option group to the mod." : "Can not add a group of this name.";
                if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Plus.ToIconString(), window._iconButtonSize,
                       tt, !nameValid, true ) )
                {
                    Penumbra.ModManager.AddModGroup( mod, SelectType.Single, _newGroupName );
                    Reset();
                }
            }
        }

        // A text input for the new directory name and a button to apply the move.
        private static class MoveDirectory
        {
            private static string?                       _currentModDirectory;
            private static Mod.Manager.NewDirectoryState _state = Mod.Manager.NewDirectoryState.Identical;

            public static void Reset()
            {
                _currentModDirectory = null;
                _state               = Mod.Manager.NewDirectoryState.Identical;
            }

            public static void Draw( Mod mod, Vector2 buttonSize )
            {
                ImGui.SetNextItemWidth( buttonSize.X * 2 + ImGui.GetStyle().ItemSpacing.X );
                var tmp = _currentModDirectory ?? mod.ModPath.Name;
                if( ImGui.InputText( "##newModMove", ref tmp, 64 ) )
                {
                    _currentModDirectory = tmp;
                    _state               = Mod.Manager.NewDirectoryValid( mod.ModPath.Name, _currentModDirectory, out _ );
                }

                var (disabled, tt) = _state switch
                {
                    Mod.Manager.NewDirectoryState.Identical      => ( true, "Current directory name is identical to new one." ),
                    Mod.Manager.NewDirectoryState.Empty          => ( true, "Please enter a new directory name first." ),
                    Mod.Manager.NewDirectoryState.NonExisting    => ( false, $"Move mod from {mod.ModPath.Name} to {_currentModDirectory}." ),
                    Mod.Manager.NewDirectoryState.ExistsEmpty    => ( false, $"Move mod from {mod.ModPath.Name} to {_currentModDirectory}." ),
                    Mod.Manager.NewDirectoryState.ExistsNonEmpty => ( true, $"{_currentModDirectory} already exists and is not empty." ),
                    Mod.Manager.NewDirectoryState.ExistsAsFile   => ( true, $"{_currentModDirectory} exists as a file." ),
                    Mod.Manager.NewDirectoryState.ContainsInvalidSymbols => ( true,
                        $"{_currentModDirectory} contains invalid symbols for FFXIV." ),
                    _ => ( true, "Unknown error." ),
                };
                ImGui.SameLine();
                if( ImGuiUtil.DrawDisabledButton( "Rename Mod Directory", buttonSize, tt, disabled ) && _currentModDirectory != null )
                {
                    Penumbra.ModManager.MoveModDirectory( mod.Index, _currentModDirectory );
                    Reset();
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(
                    "The mod directory name is used to correspond stored settings and sort orders, otherwise it has no influence on anything that is displayed.\n"
                  + "This can currently not be used on pre-existing folders and does not support merges or overwriting." );
            }
        }

        // Open a popup to edit a multi-line mod or option description.
        private static class DescriptionEdit
        {
            private const  string PopupName          = "Edit Description";
            private static string _newDescription    = string.Empty;
            private static int    _newDescriptionIdx = -1;
            private static Mod?   _mod;

            public static void OpenPopup( Mod mod, int groupIdx )
            {
                _newDescriptionIdx = groupIdx;
                _newDescription    = groupIdx < 0 ? mod.Description : mod.Groups[ groupIdx ].Description;
                _mod               = mod;
                ImGui.OpenPopup( PopupName );
            }

            public static void DrawPopup( ConfigWindow window )
            {
                if( _mod == null )
                {
                    return;
                }

                using var popup = ImRaii.Popup( PopupName );
                if( !popup )
                {
                    return;
                }

                if( ImGui.IsWindowAppearing() )
                {
                    ImGui.SetKeyboardFocusHere();
                }

                ImGui.InputTextMultiline( "##editDescription", ref _newDescription, 4096, ImGuiHelpers.ScaledVector2( 800, 800 ) );
                ImGui.Dummy( window._defaultSpace );

                var buttonSize = ImGuiHelpers.ScaledVector2( 100, 0 );
                var width = 2 * buttonSize.X
                  + 4         * ImGui.GetStyle().FramePadding.X
                  + ImGui.GetStyle().ItemSpacing.X;
                ImGui.SetCursorPosX( ( 800 * ImGuiHelpers.GlobalScale - width ) / 2 );

                var oldDescription = _newDescriptionIdx == Input.Description
                    ? _mod.Description
                    : _mod.Groups[ _newDescriptionIdx ].Description;

                var tooltip = _newDescription != oldDescription ? string.Empty : "No changes made yet.";

                if( ImGuiUtil.DrawDisabledButton( "Save", buttonSize, tooltip, tooltip.Length > 0 ) )
                {
                    switch( _newDescriptionIdx )
                    {
                        case Input.Description:
                            Penumbra.ModManager.ChangeModDescription( _mod.Index, _newDescription );
                            break;
                        case >= 0:
                            Penumbra.ModManager.ChangeGroupDescription( _mod, _newDescriptionIdx, _newDescription );
                            break;
                    }

                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();
                if( ImGui.Button( "Cancel", buttonSize )
                || ImGui.IsKeyPressed( ImGuiKey.Escape ) )
                {
                    _newDescriptionIdx = Input.None;
                    _newDescription    = string.Empty;
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        private void EditGroup( int groupIdx )
        {
            var       group = _mod.Groups[ groupIdx ];
            using var id    = ImRaii.PushId( groupIdx );
            using var frame = ImRaii.FramedGroup( $"Group #{groupIdx + 1}" );

            using var style = ImRaii.PushStyle( ImGuiStyleVar.CellPadding, _cellPadding )
               .Push( ImGuiStyleVar.ItemSpacing, _itemSpacing );

            if( Input.Text( "##Name", groupIdx, Input.None, group.Name, out var newGroupName, 256, _window._inputTextWidth.X ) )
            {
                Penumbra.ModManager.RenameModGroup( _mod, groupIdx, newGroupName );
            }

            ImGuiUtil.HoverTooltip( "Group Name" );
            ImGui.SameLine();
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), _window._iconButtonSize,
                   "Delete this option group.\nHold Control while clicking to delete.", !ImGui.GetIO().KeyCtrl, true ) )
            {
                _delayedActions.Enqueue( () => Penumbra.ModManager.DeleteModGroup( _mod, groupIdx ) );
            }

            ImGui.SameLine();

            if( Input.Priority( "##Priority", groupIdx, Input.None, group.Priority, out var priority, 50 * ImGuiHelpers.GlobalScale ) )
            {
                Penumbra.ModManager.ChangeGroupPriority( _mod, groupIdx, priority );
            }

            ImGuiUtil.HoverTooltip( "Group Priority" );

            DrawGroupCombo( group, groupIdx );
            ImGui.SameLine();

            var tt = groupIdx == 0 ? "Can not move this group further upwards." : $"Move this group up to group {groupIdx}.";
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.ArrowUp.ToIconString(), _window._iconButtonSize,
                   tt, groupIdx == 0, true ) )
            {
                _delayedActions.Enqueue( () => Penumbra.ModManager.MoveModGroup( _mod, groupIdx, groupIdx - 1 ) );
            }

            ImGui.SameLine();
            tt = groupIdx == _mod.Groups.Count - 1
                ? "Can not move this group further downwards."
                : $"Move this group down to group {groupIdx + 2}.";
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.ArrowDown.ToIconString(), _window._iconButtonSize,
                   tt, groupIdx == _mod.Groups.Count - 1, true ) )
            {
                _delayedActions.Enqueue( () => Penumbra.ModManager.MoveModGroup( _mod, groupIdx, groupIdx + 1 ) );
            }

            ImGui.SameLine();

            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Edit.ToIconString(), _window._iconButtonSize,
                   "Edit group description.", false, true ) )
            {
                _delayedActions.Enqueue( () => DescriptionEdit.OpenPopup( _mod, groupIdx ) );
            }

            ImGui.SameLine();
            var fileName   = group.FileName( _mod.ModPath, groupIdx );
            var fileExists = File.Exists( fileName );
            tt = fileExists
                ? $"Open the {group.Name} json file in the text editor of your choice."
                : $"The {group.Name} json file does not exist.";
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.FileExport.ToIconString(), _window._iconButtonSize, tt, !fileExists, true ) )
            {
                Process.Start( new ProcessStartInfo( fileName ) { UseShellExecute = true } );
            }

            ImGui.Dummy( _window._defaultSpace );

            OptionTable.Draw( this, groupIdx );
        }

        // Draw the table displaying all options and the add new option line.
        private static class OptionTable
        {
            private const string DragDropLabel = "##DragOption";

            private static int    _newOptionNameIdx  = -1;
            private static string _newOptionName     = string.Empty;
            private static int    _dragDropGroupIdx  = -1;
            private static int    _dragDropOptionIdx = -1;

            public static void Reset()
            {
                _newOptionNameIdx  = -1;
                _newOptionName     = string.Empty;
                _dragDropGroupIdx  = -1;
                _dragDropOptionIdx = -1;
            }

            public static void Draw( ModPanel panel, int groupIdx )
            {
                using var table = ImRaii.Table( string.Empty, 4, ImGuiTableFlags.SizingFixedFit );
                if( !table )
                {
                    return;
                }

                ImGui.TableSetupColumn( "idx", ImGuiTableColumnFlags.WidthFixed, 60 * ImGuiHelpers.GlobalScale );
                ImGui.TableSetupColumn( "name", ImGuiTableColumnFlags.WidthFixed,
                    panel._window._inputTextWidth.X - 62 * ImGuiHelpers.GlobalScale );
                ImGui.TableSetupColumn( "delete", ImGuiTableColumnFlags.WidthFixed, panel._window._iconButtonSize.X );
                ImGui.TableSetupColumn( "priority", ImGuiTableColumnFlags.WidthFixed, 50 * ImGuiHelpers.GlobalScale );

                var group = panel._mod.Groups[ groupIdx ];
                for( var optionIdx = 0; optionIdx < group.Count; ++optionIdx )
                {
                    EditOption( panel, group, groupIdx, optionIdx );
                }

                DrawNewOption( panel._mod, groupIdx, panel._window._iconButtonSize );
            }

            // Draw a line for a single option.
            private static void EditOption( ModPanel panel, IModGroup group, int groupIdx, int optionIdx )
            {
                var       option = group[ optionIdx ];
                using var id     = ImRaii.PushId( optionIdx );
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Selectable( $"Option #{optionIdx + 1}" );
                Source( group, groupIdx, optionIdx );
                Target( panel, group, groupIdx, optionIdx );

                ImGui.TableNextColumn();
                if( Input.Text( "##Name", groupIdx, optionIdx, option.Name, out var newOptionName, 256, -1 ) )
                {
                    Penumbra.ModManager.RenameOption( panel._mod, groupIdx, optionIdx, newOptionName );
                }

                ImGui.TableNextColumn();
                if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), panel._window._iconButtonSize,
                       "Delete this option.\nHold Control while clicking to delete.", !ImGui.GetIO().KeyCtrl, true ) )
                {
                    panel._delayedActions.Enqueue( () => Penumbra.ModManager.DeleteOption( panel._mod, groupIdx, optionIdx ) );
                }

                ImGui.TableNextColumn();
                if( group.Type == SelectType.Multi )
                {
                    if( Input.Priority( "##Priority", groupIdx, optionIdx, group.OptionPriority( optionIdx ), out var priority,
                           50 * ImGuiHelpers.GlobalScale ) )
                    {
                        Penumbra.ModManager.ChangeOptionPriority( panel._mod, groupIdx, optionIdx, priority );
                    }

                    ImGuiUtil.HoverTooltip( "Option priority." );
                }
            }

            // Draw the line to add a new option.
            private static void DrawNewOption( Mod mod, int groupIdx, Vector2 iconButtonSize )
            {
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth( -1 );
                var tmp = _newOptionNameIdx == groupIdx ? _newOptionName : string.Empty;
                if( ImGui.InputTextWithHint( "##newOption", "Add new option...", ref tmp, 256 ) )
                {
                    _newOptionName    = tmp;
                    _newOptionNameIdx = groupIdx;
                }

                ImGui.TableNextColumn();
                var canAddGroup = mod.Groups[ groupIdx ].Type != SelectType.Multi || mod.Groups[ groupIdx ].Count < IModGroup.MaxMultiOptions;
                var validName   = _newOptionName.Length       > 0 && _newOptionNameIdx                            == groupIdx;
                var tt = canAddGroup
                    ? validName ? "Add a new option to this group." : "Please enter a name for the new option."
                    : $"Can not add more than {IModGroup.MaxMultiOptions} options to a multi group.";
                if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Plus.ToIconString(), iconButtonSize,
                       tt, !( canAddGroup && validName ), true ) )
                {
                    Penumbra.ModManager.AddOption( mod, groupIdx, _newOptionName );
                    _newOptionName = string.Empty;
                }
            }

            // Handle drag and drop to move options inside a group or into another group.
            private static void Source( IModGroup group, int groupIdx, int optionIdx )
            {
                using var source = ImRaii.DragDropSource();
                if( !source )
                {
                    return;
                }

                if( ImGui.SetDragDropPayload( DragDropLabel, IntPtr.Zero, 0 ) )
                {
                    _dragDropGroupIdx  = groupIdx;
                    _dragDropOptionIdx = optionIdx;
                }

                ImGui.TextUnformatted( $"Dragging option {group[ optionIdx ].Name} from group {group.Name}..." );
            }

            private static void Target( ModPanel panel, IModGroup group, int groupIdx, int optionIdx )
            {
                // TODO drag options to other groups without options.
                using var target = ImRaii.DragDropTarget();
                if( !target.Success || !ImGuiUtil.IsDropping( DragDropLabel ) )
                {
                    return;
                }

                if( _dragDropGroupIdx >= 0 && _dragDropOptionIdx >= 0 )
                {
                    if( _dragDropGroupIdx == groupIdx )
                    {
                        var sourceOption = _dragDropOptionIdx;
                        panel._delayedActions.Enqueue( () => Penumbra.ModManager.MoveOption( panel._mod, groupIdx, sourceOption, optionIdx ) );
                    }
                    else
                    {
                        // Move from one group to another by deleting, then adding the option.
                        var sourceGroup  = _dragDropGroupIdx;
                        var sourceOption = _dragDropOptionIdx;
                        var option       = group[ _dragDropOptionIdx ];
                        var priority     = group.OptionPriority( _dragDropGroupIdx );
                        panel._delayedActions.Enqueue( () =>
                        {
                            Penumbra.ModManager.DeleteOption( panel._mod, sourceGroup, sourceOption );
                            Penumbra.ModManager.AddOption( panel._mod, groupIdx, option, priority );
                        } );
                    }
                }

                _dragDropGroupIdx  = -1;
                _dragDropOptionIdx = -1;
            }
        }

        // Draw a combo to select single or multi group and switch between them.
        private void DrawGroupCombo( IModGroup group, int groupIdx )
        {
            static string GroupTypeName( SelectType type )
                => type switch
                {
                    SelectType.Single => "Single Group",
                    SelectType.Multi  => "Multi Group",
                    _                 => "Unknown",
                };

            ImGui.SetNextItemWidth( _window._inputTextWidth.X - 3 * _window._iconButtonSize.X - 12 * ImGuiHelpers.GlobalScale );
            using var combo = ImRaii.Combo( "##GroupType", GroupTypeName( group.Type ) );
            if( !combo )
            {
                return;
            }

            if( ImGui.Selectable( GroupTypeName( SelectType.Single ), group.Type == SelectType.Single ) )
            {
                Penumbra.ModManager.ChangeModGroupType( _mod, groupIdx, SelectType.Single );
            }

            var       canSwitchToMulti = group.Count <= IModGroup.MaxMultiOptions;
            using var style            = ImRaii.PushStyle( ImGuiStyleVar.Alpha, 0.5f, !canSwitchToMulti );
            if( ImGui.Selectable( GroupTypeName( SelectType.Multi ), group.Type == SelectType.Multi ) && canSwitchToMulti )
            {
                Penumbra.ModManager.ChangeModGroupType( _mod, groupIdx, SelectType.Multi );
            }

            style.Pop();
            if( !canSwitchToMulti )
            {
                ImGuiUtil.HoverTooltip( $"Can not convert group to multi group since it has more than {IModGroup.MaxMultiOptions} options." );
            }
        }

        // Handles input text and integers in separate fields without buffers for every single one.
        private static class Input
        {
            // Special field indices to reuse the same string buffer.
            public const int None        = -1;
            public const int Name        = -2;
            public const int Author      = -3;
            public const int Version     = -4;
            public const int Website     = -5;
            public const int Path        = -6;
            public const int Description = -7;

            // Temporary strings
            private static string? _currentEdit;
            private static int?    _currentGroupPriority;
            private static int     _currentField = None;
            private static int     _optionIndex  = None;

            public static void Reset()
            {
                _currentEdit          = null;
                _currentGroupPriority = null;
                _currentField         = None;
                _optionIndex          = None;
            }

            public static bool Text( string label, int field, int option, string oldValue, out string value, uint maxLength, float width )
            {
                var tmp = field == _currentField && option == _optionIndex ? _currentEdit ?? oldValue : oldValue;
                ImGui.SetNextItemWidth( width );
                if( ImGui.InputText( label, ref tmp, maxLength ) )
                {
                    _currentEdit  = tmp;
                    _optionIndex  = option;
                    _currentField = field;
                }

                if( ImGui.IsItemDeactivatedAfterEdit() && _currentEdit != null )
                {
                    var ret = _currentEdit != oldValue;
                    value = _currentEdit;
                    Reset();
                    return ret;
                }

                value = string.Empty;
                return false;
            }

            public static bool Priority( string label, int field, int option, int oldValue, out int value, float width )
            {
                var tmp = field == _currentField && option == _optionIndex ? _currentGroupPriority ?? oldValue : oldValue;
                ImGui.SetNextItemWidth( width );
                if( ImGui.InputInt( label, ref tmp, 0, 0 ) )
                {
                    _currentGroupPriority = tmp;
                    _optionIndex          = option;
                    _currentField         = field;
                }

                if( ImGui.IsItemDeactivatedAfterEdit() && _currentGroupPriority != null )
                {
                    var ret = _currentGroupPriority != oldValue;
                    value = _currentGroupPriority.Value;
                    Reset();
                    return ret;
                }

                value = 0;
                return false;
            }
        }
    }
}