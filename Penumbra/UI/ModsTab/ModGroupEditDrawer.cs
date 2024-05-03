using Dalamud.Interface;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Services;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public sealed class ModGroupEditDrawer(ModManager modManager, Configuration config, FilenameService filenames) : IUiService
{
    private Vector2 _buttonSize;
    private float   _priorityWidth;
    private float   _groupNameWidth;
    private float   _spacing;

    private string?      _currentGroupName;
    private ModPriority? _currentGroupPriority;
    private IModGroup?   _currentGroupEdited;
    private bool         _isGroupNameValid;
    private IModGroup?   _deleteGroup;
    private IModGroup?   _moveGroup;
    private int          _moveTo;

    private string?      _currentOptionName;
    private ModPriority? _currentOptionPriority;
    private IModOption?  _currentOptionEdited;
    private IModOption?  _deleteOption;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SameLine()
        => ImGui.SameLine(0, _spacing);

    public void Draw(Mod mod)
    {
        _buttonSize     = new Vector2(ImGui.GetFrameHeight());
        _priorityWidth  = 50 * ImGuiHelpers.GlobalScale;
        _groupNameWidth = 350f * ImGuiHelpers.GlobalScale;
        _spacing        = ImGui.GetStyle().ItemInnerSpacing.X;


        FinishGroupCleanup();
    }

    private void FinishGroupCleanup()
    {
        if (_deleteGroup != null)
        {
            modManager.OptionEditor.DeleteModGroup(_deleteGroup);
            _deleteGroup = null;
        }

        if (_deleteOption != null)
        {
            modManager.OptionEditor.DeleteOption(_deleteOption);
            _deleteOption = null;
        }

        if (_moveGroup != null)
        {
            modManager.OptionEditor.MoveModGroup(_moveGroup, _moveTo);
            _moveGroup = null;
        }
    }

    private void DrawGroup(IModGroup group, int idx)
    {
        using var id    = ImRaii.PushId(idx);
        using var frame = ImRaii.FramedGroup($"Group #{idx + 1}");
        DrawGroupNameRow(group);
        switch (group)
        {
            case SingleModGroup s:
                DrawSingleGroup(s, idx);
                break;
            case MultiModGroup m:
                DrawMultiGroup(m, idx);
                break;
            case ImcModGroup i:
                DrawImcGroup(i, idx);
                break;
        }
    }

    private void DrawGroupNameRow(IModGroup group)
    {
        DrawGroupName(group);
        SameLine();
        DrawGroupDelete(group);
        SameLine();
        DrawGroupPriority(group);
    }

    private void DrawGroupName(IModGroup group)
    {
        var text = _currentGroupEdited == group ? _currentGroupName ?? group.Name : group.Name;
        ImGui.SetNextItemWidth(_groupNameWidth);
        using var border = ImRaii.PushFrameBorder(UiHelpers.ScaleX2, Colors.RegexWarningBorder, !_isGroupNameValid);
        if (ImGui.InputText("##GroupName", ref text, 256))
        {
            _currentGroupEdited = group;
            _currentGroupName   = text;
            _isGroupNameValid   = ModGroupEditor.VerifyFileName(group.Mod, group, text, false);
        }

        if (ImGui.IsItemDeactivated())
        {
            if (_currentGroupName != null && _isGroupNameValid)
                modManager.OptionEditor.RenameModGroup(group, _currentGroupName);
            _currentGroupName   = null;
            _currentGroupEdited = null;
            _isGroupNameValid   = true;
        }

        var tt = _isGroupNameValid
            ? "Group Name"
            : "Current name can not be used for this group.";
        ImGuiUtil.HoverTooltip(tt);
    }

    private void DrawGroupDelete(IModGroup group)
    {
        var enabled = config.DeleteModModifier.IsActive();
        var tt = enabled
            ? "Delete this option group."
            : $"Delete this option group.\nHold {config.DeleteModModifier} while clicking to delete.";

        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), _buttonSize, tt, !enabled, true))
            _deleteGroup = group;
    }

    private void DrawGroupPriority(IModGroup group)
    {
        var priority = _currentGroupEdited == group
            ? (_currentGroupPriority ?? group.Priority).Value
            : group.Priority.Value;
        ImGui.SetNextItemWidth(_priorityWidth);
        if (ImGui.InputInt("##GroupPriority", ref priority, 0, 0))
        {
            _currentGroupEdited   = group;
            _currentGroupPriority = new ModPriority(priority);
        }

        if (ImGui.IsItemDeactivated())
        {
            if (_currentGroupPriority.HasValue)
                modManager.OptionEditor.ChangeGroupPriority(group, _currentGroupPriority.Value);
            _currentGroupEdited   = null;
            _currentGroupPriority = null;
        }

        ImGuiUtil.HoverTooltip("Group Priority");
    }

    private void DrawGroupMoveButtons(IModGroup group, int idx)
    {
        var isFirst = idx == 0;
        var tt      = isFirst ? "Can not move this group further upwards." : $"Move this group up to group {idx}.";
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.ArrowUp.ToIconString(), UiHelpers.IconButtonSize, tt, isFirst, true))
        {
            _moveGroup = group;
            _moveTo    = idx - 1;
        }

        SameLine();
        var isLast = idx == group.Mod.Groups.Count - 1;
        tt = isLast
            ? "Can not move this group further downwards."
            : $"Move this group down to group {idx + 2}.";
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.ArrowDown.ToIconString(), UiHelpers.IconButtonSize, tt, isLast, true))
        {
            _moveGroup = group;
            _moveTo    = idx + 1;
        }
    }

    private void DrawGroupOpenFile(IModGroup group, int idx)
    {
        var fileName   = filenames.OptionGroupFile(group.Mod, idx, config.ReplaceNonAsciiOnImport);
        var fileExists = File.Exists(fileName);
        var tt = fileExists
            ? $"Open the {group.Name} json file in the text editor of your choice."
            : $"The {group.Name} json file does not exist.";
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.FileExport.ToIconString(), UiHelpers.IconButtonSize, tt, !fileExists, true))
            try
            {
                Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
            }
            catch (Exception e)
            {
                Penumbra.Messager.NotificationMessage(e, "Could not open editor.", NotificationType.Error);
            }
    }


    private void DrawSingleGroup(SingleModGroup group, int idx)
    { }

    private void DrawMultiGroup(MultiModGroup group, int idx)
    { }

    private void DrawImcGroup(ImcModGroup group, int idx)
    { }
}
