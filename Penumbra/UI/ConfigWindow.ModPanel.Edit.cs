using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Mods;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private partial class ModPanel
    {
        private readonly Queue< Action > _delayedActions = new();

        private void DrawAddOptionGroupInput()
        {
            ImGui.SetNextItemWidth( _window._inputTextWidth.X );
            ImGui.InputTextWithHint( "##newGroup", "Add new option group...", ref _newGroupName, 256 );
            ImGui.SameLine();

            var nameValid = Mod.Manager.VerifyFileName( _mod, null, _newGroupName, false );
            var tt        = nameValid ? "Add new option group to the mod." : "Can not add a group of this name.";
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Plus.ToIconString(), _window._iconButtonSize,
                   tt, !nameValid, true ) )
            {
                Penumbra.ModManager.AddModGroup( _mod, SelectType.Single, _newGroupName );
                _newGroupName = string.Empty;
            }
        }

        private Vector2 _cellPadding = Vector2.Zero;
        private Vector2 _itemSpacing = Vector2.Zero;

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

            if( TextInput( "Mod Path", PathFieldIdx, NoFieldIdx, _leaf.FullName(), out var newPath, 256, _window._inputTextWidth.X ) )
            {
                _window._penumbra.ModFileSystem.RenameAndMove( _leaf, newPath );
            }

            ImGui.Dummy( _window._defaultSpace );
            DrawAddOptionGroupInput();
            ImGui.Dummy( _window._defaultSpace );

            for( var groupIdx = 0; groupIdx < _mod.Groups.Count; ++groupIdx )
            {
                EditGroup( groupIdx );
            }

            EndActions();
            EditDescriptionPopup();
        }

        private void EditButtons()
        {
            var buttonSize   = new Vector2( 150 * ImGuiHelpers.GlobalScale, 0 );
            var folderExists = Directory.Exists( _mod.BasePath.FullName );
            var tt = folderExists
                ? $"Open \"{_mod.BasePath.FullName}\" in the file explorer of your choice."
                : $"Mod directory \"{_mod.BasePath.FullName}\" does not exist.";
            if( ImGuiUtil.DrawDisabledButton( "Open Mod Directory", buttonSize, tt, !folderExists ) )
            {
                Process.Start( new ProcessStartInfo( _mod.BasePath.FullName ) { UseShellExecute = true } );
            }


            ImGui.SameLine();
            ImGuiUtil.DrawDisabledButton( "Rename Mod Directory", buttonSize, "Not implemented yet", true );
            ImGui.SameLine();
            ImGuiUtil.DrawDisabledButton( "Reload Mod", buttonSize, "Not implemented yet", true );

            ImGuiUtil.DrawDisabledButton( "Deduplicate", buttonSize, "Not implemented yet", true );
            ImGui.SameLine();
            ImGuiUtil.DrawDisabledButton( "Normalize", buttonSize, "Not implemented yet", true );
            ImGui.SameLine();
            ImGuiUtil.DrawDisabledButton( "Auto-Create Groups", buttonSize, "Not implemented yet", true );

            ImGuiUtil.DrawDisabledButton( "Change Material Suffix", buttonSize, "Not implemented yet", true );

            ImGui.Dummy( _window._defaultSpace );
        }


        // Special field indices to reuse the same string buffer.
        private const int NoFieldIdx          = -1;
        private const int NameFieldIdx        = -2;
        private const int AuthorFieldIdx      = -3;
        private const int VersionFieldIdx     = -4;
        private const int WebsiteFieldIdx     = -5;
        private const int PathFieldIdx        = -6;
        private const int DescriptionFieldIdx = -7;

        private void EditRegularMeta()
        {
            if( TextInput( "Name", NameFieldIdx, NoFieldIdx, _mod.Name, out var newName, 256, _window._inputTextWidth.X ) )
            {
                Penumbra.ModManager.ChangeModName( _mod.Index, newName );
            }

            if( TextInput( "Author", AuthorFieldIdx, NoFieldIdx, _mod.Author, out var newAuthor, 256, _window._inputTextWidth.X ) )
            {
                Penumbra.ModManager.ChangeModAuthor( _mod.Index, newAuthor );
            }

            if( TextInput( "Version", VersionFieldIdx, NoFieldIdx, _mod.Version, out var newVersion, 32, _window._inputTextWidth.X ) )
            {
                Penumbra.ModManager.ChangeModVersion( _mod.Index, newVersion );
            }

            if( TextInput( "Website", WebsiteFieldIdx, NoFieldIdx, _mod.Website, out var newWebsite, 256, _window._inputTextWidth.X ) )
            {
                Penumbra.ModManager.ChangeModWebsite( _mod.Index, newWebsite );
            }

            var       spacing = new Vector2( 3 * ImGuiHelpers.GlobalScale );
            using var style   = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, spacing );

            var reducedSize = new Vector2( _window._inputTextWidth.X - _window._iconButtonSize.X - spacing.X, 0 );
            if( ImGui.Button( "Edit Description", reducedSize ) )
            {
                _delayedActions.Enqueue( () => OpenEditDescriptionPopup( DescriptionFieldIdx ) );
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

            if( ImGui.Button( "Edit Default Mod", reducedSize ) )
            {
                _window.SubModPopup.Activate( _mod, -1, 0 );
            }

            ImGui.SameLine();
            fileExists = File.Exists( _mod.DefaultFile );
            tt = fileExists
                ? "Open the default option json file in the text editor of your choice."
                : "The default option json file does not exist.";
            if( ImGuiUtil.DrawDisabledButton( $"{FontAwesomeIcon.FileExport.ToIconString()}##defaultFile", _window._iconButtonSize, tt,
                   !fileExists, true ) )
            {
                Process.Start( new ProcessStartInfo( _mod.DefaultFile ) { UseShellExecute = true } );
            }
        }


        // Temporary strings
        private string? _currentEdit;
        private int?    _currentGroupPriority;
        private int     _currentField = -1;
        private int     _optionIndex  = -1;

        private string _newGroupName      = string.Empty;
        private string _newOptionName     = string.Empty;
        private string _newDescription    = string.Empty;
        private int    _newDescriptionIdx = -1;

        private void EditGroup( int groupIdx )
        {
            var       group = _mod.Groups[ groupIdx ];
            using var id    = ImRaii.PushId( groupIdx );
            using var frame = ImRaii.FramedGroup( $"Group #{groupIdx + 1}" );

            using var style = ImRaii.PushStyle( ImGuiStyleVar.CellPadding, _cellPadding )
               .Push( ImGuiStyleVar.ItemSpacing, _itemSpacing );

            if( TextInput( "##Name", groupIdx, NoFieldIdx, group.Name, out var newGroupName, 256, _window._inputTextWidth.X ) )
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

            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Edit.ToIconString(), _window._iconButtonSize,
                   "Edit group description.", false, true ) )
            {
                _delayedActions.Enqueue( () => OpenEditDescriptionPopup( groupIdx ) );
            }

            ImGui.SameLine();

            if( PriorityInput( "##Priority", groupIdx, NoFieldIdx, group.Priority, out var priority, 50 * ImGuiHelpers.GlobalScale ) )
            {
                Penumbra.ModManager.ChangeGroupPriority( _mod, groupIdx, priority );
            }

            ImGuiUtil.HoverTooltip( "Group Priority" );

            ImGui.SetNextItemWidth( _window._inputTextWidth.X - 3 * _window._iconButtonSize.X - 12 * ImGuiHelpers.GlobalScale );
            using( var combo = ImRaii.Combo( "##GroupType", GroupTypeName( group.Type ) ) )
            {
                if( combo )
                {
                    foreach( var type in new[] { SelectType.Single, SelectType.Multi } )
                    {
                        if( ImGui.Selectable( GroupTypeName( type ), group.Type == type ) )
                        {
                            Penumbra.ModManager.ChangeModGroupType( _mod, groupIdx, type );
                        }
                    }
                }
            }

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
            var fileName   = group.FileName( _mod.BasePath );
            var fileExists = File.Exists( fileName );
            tt = fileExists
                ? $"Open the {group.Name} json file in the text editor of your choice."
                : $"The {group.Name} json file does not exist.";
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.FileExport.ToIconString(), _window._iconButtonSize, tt, !fileExists, true ) )
            {
                Process.Start( new ProcessStartInfo( fileName ) { UseShellExecute = true } );
            }

            ImGui.Dummy( _window._defaultSpace );

            using var table = ImRaii.Table( string.Empty, 5, ImGuiTableFlags.SizingFixedFit );
            ImGui.TableSetupColumn( "idx", ImGuiTableColumnFlags.WidthFixed, 60 * ImGuiHelpers.GlobalScale );
            ImGui.TableSetupColumn( "name", ImGuiTableColumnFlags.WidthFixed, _window._inputTextWidth.X - 62 * ImGuiHelpers.GlobalScale );
            ImGui.TableSetupColumn( "delete", ImGuiTableColumnFlags.WidthFixed, _window._iconButtonSize.X );
            ImGui.TableSetupColumn( "edit", ImGuiTableColumnFlags.WidthFixed, _window._iconButtonSize.X );
            ImGui.TableSetupColumn( "priority", ImGuiTableColumnFlags.WidthFixed, 50 * ImGuiHelpers.GlobalScale );
            if( table )
            {
                for( var optionIdx = 0; optionIdx < group.Count; ++optionIdx )
                {
                    EditOption( group, groupIdx, optionIdx );
                }

                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth( -1 );
                ImGui.InputTextWithHint( "##newOption", "Add new option...", ref _newOptionName, 256 );
                ImGui.TableNextColumn();
                if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Plus.ToIconString(), _window._iconButtonSize,
                       "Add a new option to this group.", _newOptionName.Length == 0, true ) )
                {
                    Penumbra.ModManager.AddOption( _mod, groupIdx, _newOptionName );
                    _newOptionName = string.Empty;
                }
            }
        }

        private static string GroupTypeName( SelectType type )
            => type switch
            {
                SelectType.Single => "Single Group",
                SelectType.Multi  => "Multi Group",
                _                 => "Unknown",
            };

        private int _dragDropGroupIdx  = -1;
        private int _dragDropOptionIdx = -1;

        private void OptionDragDrop( IModGroup group, int groupIdx, int optionIdx )
        {
            const string label = "##DragOption";
            using( var source = ImRaii.DragDropSource() )
            {
                if( source )
                {
                    if( ImGui.SetDragDropPayload( label, IntPtr.Zero, 0 ) )
                    {
                        _dragDropGroupIdx  = groupIdx;
                        _dragDropOptionIdx = optionIdx;
                    }

                    ImGui.Text( $"Dragging option {group[ optionIdx ].Name} from group {group.Name}..." );
                }
            }

            // TODO drag options to other groups without options.
            using( var target = ImRaii.DragDropTarget() )
            {
                if( target.Success && ImGuiUtil.IsDropping( label ) )
                {
                    if( _dragDropGroupIdx >= 0 && _dragDropOptionIdx >= 0 )
                    {
                        if( _dragDropGroupIdx == groupIdx )
                        {
                            var sourceOption = _dragDropOptionIdx;
                            _delayedActions.Enqueue( () => Penumbra.ModManager.MoveOption( _mod, groupIdx, sourceOption, optionIdx ) );
                        }
                        else
                        {
                            // Move from one group to another by deleting, then adding the option.
                            var sourceGroup  = _dragDropGroupIdx;
                            var sourceOption = _dragDropOptionIdx;
                            var option       = group[ _dragDropOptionIdx ];
                            var priority     = group.OptionPriority( _dragDropGroupIdx );
                            _delayedActions.Enqueue( () =>
                            {
                                Penumbra.ModManager.DeleteOption( _mod, sourceGroup, sourceOption );
                                Penumbra.ModManager.AddOption( _mod, groupIdx, option, priority );
                            } );
                        }
                    }

                    _dragDropGroupIdx  = -1;
                    _dragDropOptionIdx = -1;
                }
            }
        }

        private void EditOption( IModGroup group, int groupIdx, int optionIdx )
        {
            var       option = group[ optionIdx ];
            using var id     = ImRaii.PushId( optionIdx );
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Selectable( $"Option #{optionIdx + 1}" );
            OptionDragDrop( group, groupIdx, optionIdx );

            ImGui.TableNextColumn();
            if( TextInput( "##Name", groupIdx, optionIdx, option.Name, out var newOptionName, 256, -1 ) )
            {
                Penumbra.ModManager.RenameOption( _mod, groupIdx, optionIdx, newOptionName );
            }

            ImGui.TableNextColumn();
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), _window._iconButtonSize,
                   "Delete this option.\nHold Control while clicking to delete.", !ImGui.GetIO().KeyCtrl, true ) )
            {
                _delayedActions.Enqueue( () => Penumbra.ModManager.DeleteOption( _mod, groupIdx, optionIdx ) );
            }

            ImGui.TableNextColumn();
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Edit.ToIconString(), _window._iconButtonSize,
                   "Edit this option.", false, true ) )
            {
                _window.SubModPopup.Activate( _mod, groupIdx, optionIdx );
            }

            ImGui.TableNextColumn();
            if( group.Type == SelectType.Multi )
            {
                if( PriorityInput( "##Priority", groupIdx, optionIdx, group.OptionPriority( optionIdx ), out var priority,
                       50 * ImGuiHelpers.GlobalScale ) )
                {
                    Penumbra.ModManager.ChangeOptionPriority( _mod, groupIdx, optionIdx, priority );
                }

                ImGuiUtil.HoverTooltip( "Option priority." );
            }
        }

        private bool TextInput( string label, int field, int option, string oldValue, out string value, uint maxLength, float width )
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
                value         = _currentEdit;
                _currentEdit  = null;
                _currentField = NoFieldIdx;
                _optionIndex  = NoFieldIdx;
                return ret;
            }

            value = string.Empty;
            return false;
        }

        private bool PriorityInput( string label, int field, int option, int oldValue, out int value, float width )
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
                value                 = _currentGroupPriority.Value;
                _currentGroupPriority = null;
                _currentField         = NoFieldIdx;
                _optionIndex          = NoFieldIdx;
                return ret;
            }

            value = 0;
            return false;
        }

        // Delete a marked group or option outside of iteration.
        private void EndActions()
        {
            while( _delayedActions.TryDequeue( out var action ) )
            {
                action.Invoke();
            }
        }

        private void OpenEditDescriptionPopup( int groupIdx )
        {
            _newDescriptionIdx = groupIdx;
            _newDescription    = groupIdx < 0 ? _mod.Description : _mod.Groups[ groupIdx ].Description;
            ImGui.OpenPopup( "Edit Description" );
        }

        private void EditDescriptionPopup()
        {
            using var popup = ImRaii.Popup( "Edit Description" );
            if( popup )
            {
                if( ImGui.IsWindowAppearing() )
                {
                    ImGui.SetKeyboardFocusHere();
                }

                ImGui.InputTextMultiline( "##editDescription", ref _newDescription, 4096, ImGuiHelpers.ScaledVector2( 800, 800 ) );
                ImGui.Dummy( _window._defaultSpace );

                var buttonSize = ImGuiHelpers.ScaledVector2( 100, 0 );
                var width = 2 * buttonSize.X
                  + 4         * ImGui.GetStyle().FramePadding.X
                  + ImGui.GetStyle().ItemSpacing.X;
                ImGui.SetCursorPosX( ( 800 * ImGuiHelpers.GlobalScale - width ) / 2 );

                var oldDescription = _newDescriptionIdx == DescriptionFieldIdx
                    ? _mod.Description
                    : _mod.Groups[ _newDescriptionIdx ].Description;

                var tooltip = _newDescription != oldDescription ? string.Empty : "No changes made yet.";

                if( ImGuiUtil.DrawDisabledButton( "Save", buttonSize, tooltip, tooltip.Length > 0 ) )
                {
                    if( _newDescriptionIdx == DescriptionFieldIdx )
                    {
                        Penumbra.ModManager.ChangeModDescription( _mod.Index, _newDescription );
                    }
                    else if( _newDescriptionIdx >= 0 )
                    {
                        Penumbra.ModManager.ChangeGroupDescription( _mod, _newDescriptionIdx, _newDescription );
                    }

                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();
                if( ImGui.Button( "Cancel", buttonSize )
                || ImGui.IsKeyPressed( ImGui.GetKeyIndex( ImGuiKey.Escape ) ) )
                {
                    _newDescriptionIdx = NoFieldIdx;
                    _newDescription    = string.Empty;
                    ImGui.CloseCurrentPopup();
                }
            }
        }
    }
}