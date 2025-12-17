using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using ImSharp;
using Luna;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab.Groups;

public sealed class ModGroupEditDrawer(
    ModManager modManager,
    Configuration config,
    FilenameService filenames,
    DescriptionEditPopup descriptionPopup,
    ImcChecker imcChecker) : IUiService
{
    private static ReadOnlySpan<byte> AcrossGroupsLabel
        => "##DragOptionAcross"u8;

    private static ReadOnlySpan<byte> InsideGroupLabel
        => "##DragOptionInside"u8;

    internal readonly ImcChecker    ImcChecker  = imcChecker;
    internal readonly ModManager    ModManager  = modManager;
    internal readonly Queue<Action> ActionQueue = new();

    internal Vector2 OptionIdxSelectable;
    internal Vector2 AvailableWidth;
    internal float   PriorityWidth;

    internal string?    NewOptionName;
    private  IModGroup? _newOptionGroup;

    private Vector2 _buttonSize;
    private float   _groupNameWidth;
    private float   _optionNameWidth;
    private float   _spacing;
    private bool    _deleteEnabled;

    private string?      _currentGroupName;
    private IModGroup?   _currentGroupEdited;
    private bool         _isGroupNameValid = true;

    private IModGroup?  _dragDropGroup;
    private IModOption? _dragDropOption;
    private bool        _draggingAcross;

    public void Draw(Mod mod)
    {
        PrepareStyle();

        using var id = Im.Id.Push("ge"u8);
        foreach (var (groupIdx, group) in mod.Groups.Index())
            DrawGroup(group, groupIdx);

        while (ActionQueue.TryDequeue(out var action))
            action.Invoke();
    }

    private void DrawGroup(IModGroup group, int idx)
    {
        using var id    = Im.Id.Push(idx);
        using var frame = ImEx.FramedGroup($"Group #{idx + 1}");
        DrawGroupNameRow(group, idx);
        group.EditDrawer(this).Draw();
    }

    private void DrawGroupNameRow(IModGroup group, int idx)
    {
        DrawGroupName(group);
        Im.Line.SameInner();
        DrawGroupMoveButtons(group, idx);
        Im.Line.SameInner();
        DrawGroupOpenFile(group, idx);
        Im.Line.SameInner();
        DrawGroupDescription(group);
        Im.Line.SameInner();
        DrawGroupDelete(group);
        Im.Line.SameInner();
        DrawGroupPriority(group);
    }

    private void DrawGroupName(IModGroup group)
    {
        var text = _currentGroupEdited == group ? _currentGroupName ?? group.Name : group.Name;
        Im.Item.SetNextWidth(_groupNameWidth);
        using var border = ImStyleBorder.Frame.Push(Colors.RegexWarningBorder, Im.Style.GlobalScale * 2, !_isGroupNameValid);
        if (Im.Input.Text("##GroupName"u8, ref text))
        {
            _currentGroupEdited = group;
            _currentGroupName   = text;
            _isGroupNameValid   = text == group.Name || ModGroupEditor.VerifyFileName(group.Mod, group, text, false);
        }

        if (Im.Item.Deactivated)
        {
            if (_currentGroupName != null && _isGroupNameValid)
                ModManager.OptionEditor.RenameModGroup(group, _currentGroupName);
            _currentGroupName   = null;
            _currentGroupEdited = null;
            _isGroupNameValid   = true;
        }

        var tt = _isGroupNameValid
            ? "Change the Group name."u8
            : "Current name can not be used for this group."u8;
        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, tt);
    }

    private void DrawGroupDelete(IModGroup group)
    {
        if (ImEx.Icon.Button(LunaStyle.DeleteIcon, !_deleteEnabled))
            ActionQueue.Enqueue(() => ModManager.OptionEditor.DeleteModGroup(group));

        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "Delete this option group."u8);
        if (!_deleteEnabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"Hold {config.DeleteModModifier} while clicking to delete.");
    }

    private void DrawGroupPriority(IModGroup group)
    {
        Im.Item.SetNextWidth(PriorityWidth);
        if (ImEx.InputOnDeactivation.Scalar("##GroupPriority"u8, group.Priority.Value, out var newPriority))
            ModManager.OptionEditor.ChangeGroupPriority(group, new ModPriority(newPriority));
        Im.Tooltip.OnHover("Group Priority"u8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawGroupDescription(IModGroup group)
    {
        if (ImEx.Icon.Button(LunaStyle.EditIcon, "Edit group description."u8))
            descriptionPopup.Open(group);
    }

    private void DrawGroupMoveButtons(IModGroup group, int idx)
    {
        var isFirst = idx is 0;
        if (ImEx.Icon.Button(FontAwesomeIcon.ArrowUp.Icon(), isFirst))
            ActionQueue.Enqueue(() => ModManager.OptionEditor.MoveModGroup(group, idx - 1));

        if (isFirst)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "Can not move this group further upwards."u8);
        else
            Im.Tooltip.OnHover($"Move this group up to group {idx}.");


        Im.Line.SameInner();
        var isLast = idx == group.Mod.Groups.Count - 1;
        if (ImEx.Icon.Button(FontAwesomeIcon.ArrowDown.Icon(), isLast))
            ActionQueue.Enqueue(() => ModManager.OptionEditor.MoveModGroup(group, idx + 1));

        if (isLast)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "Can not move this group further downwards."u8);
        else
            Im.Tooltip.OnHover($"Move this group down to group {idx + 2}.");
    }

    private void DrawGroupOpenFile(IModGroup group, int idx)
    {
        var fileName   = filenames.OptionGroupFile(group.Mod, idx, config.ReplaceNonAsciiOnImport);
        var fileExists = File.Exists(fileName);
        if (ImEx.Icon.Button(LunaStyle.OpenExternalIcon, !fileExists))
            try
            {
                Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
            }
            catch (Exception e)
            {
                Penumbra.Messager.NotificationMessage(e, "Could not open editor.", NotificationType.Error);
            }

        if (fileExists)
            Im.Tooltip.OnHover($"Open the {group.Name} json file in the text editor of your choice.");
        else
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"The {group.Name} json file does not exist.");
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionPosition(IModGroup group, IModOption option, int optionIdx)
    {
        Im.Cursor.FrameAlign();
        Im.Selectable($"Option #{optionIdx + 1}", size: OptionIdxSelectable);
        Target(group, optionIdx);
        Source(option);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionDefaultSingleBehaviour(IModGroup group, IModOption option, int optionIdx)
    {
        var isDefaultOption = group.DefaultSettings.AsIndex == optionIdx;
        if (Im.RadioButton("##default"u8, isDefaultOption))
            ModManager.OptionEditor.ChangeModGroupDefaultOption(group, Setting.Single(optionIdx));
        Im.Tooltip.OnHover($"Set {option.Name} as the default choice for this group.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionDefaultMultiBehaviour(IModGroup group, IModOption option, int optionIdx)
    {
        var isDefaultOption = group.DefaultSettings.HasFlag(optionIdx);
        if (Im.Checkbox("##default"u8, ref isDefaultOption))
            ModManager.OptionEditor.ChangeModGroupDefaultOption(group, group.DefaultSettings.SetBit(optionIdx, isDefaultOption));
        Im.Tooltip.OnHover($"{(isDefaultOption ? "Disable"u8 : "Enable"u8)} {option.Name} per default in this group.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionDescription(IModOption option)
    {
        if (ImEx.Icon.Button(LunaStyle.EditIcon, "Edit option description."u8))
            descriptionPopup.Open(option);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionPriority(MultiSubMod option)
    {
        Im.Item.SetNextWidth(PriorityWidth);
        if (ImEx.InputOnDeactivation.Scalar("##Priority"u8, option.Priority.Value, out var newValue))
            ModManager.OptionEditor.MultiEditor.ChangeOptionPriority(option, new ModPriority(newValue));
        Im.Tooltip.OnHover("Option priority inside the mod."u8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionName(IModOption option)
    {
        Im.Item.SetNextWidth(_optionNameWidth);
        if (ImEx.InputOnDeactivation.Text("##Name"u8, option.Name, out string newName))
            ModManager.OptionEditor.RenameOption(option, newName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DrawOptionDelete(IModOption option)
    {
        if (ImEx.Icon.Button(LunaStyle.DeleteIcon, !_deleteEnabled))
            ActionQueue.Enqueue(() => ModManager.OptionEditor.DeleteOption(option));

        if (_deleteEnabled)
            Im.Tooltip.OnHover("Delete this option."u8);
        else
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled,
                $"Delete this option.\nHold {config.DeleteModModifier} while clicking to delete.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string DrawNewOptionBase(IModGroup group, int count)
    {
        Im.Cursor.FrameAlign();
        Im.Selectable($"Option #{count + 1}", size: OptionIdxSelectable);
        Target(group, count);

        Im.Line.SameInner();
        Im.FrameDummy();

        Im.Line.SameInner();
        Im.Item.SetNextWidth(_optionNameWidth);
        var newName = _newOptionGroup == group
            ? NewOptionName ?? string.Empty
            : string.Empty;
        if (Im.Input.Text("##newOption"u8, ref newName, "Add new option..."u8))
        {
            NewOptionName   = newName;
            _newOptionGroup = group;
        }

        Im.Line.SameInner();
        return newName;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Source(IModOption option)
    {
        using var source = Im.DragDrop.Source();
        if (!source)
            return;

        var across = option.Group is ITexToolsGroup;

        if (!source.SetPayload(across ? AcrossGroupsLabel : InsideGroupLabel))
        {
            _dragDropGroup  = option.Group;
            _dragDropOption = option;
            _draggingAcross = across;
        }

        Im.Text($"Dragging option {option.Name} from group {option.Group.Name}...");
    }

    private void Target(IModGroup group, int optionIdx)
    {
        if (_dragDropGroup != group
         && (!_draggingAcross || _dragDropGroup is not null && group is MultiModGroup { Options.Count: >= IModGroup.MaxMultiOptions }))
            return;

        using var target = Im.DragDrop.Target();
        if (!target.IsDropping(_draggingAcross ? AcrossGroupsLabel : InsideGroupLabel))
            return;

        if (_dragDropGroup is not null && _dragDropOption is not null)
        {
            if (_dragDropGroup == group)
            {
                var sourceOption = _dragDropOption;
                ActionQueue.Enqueue(() => ModManager.OptionEditor.MoveOption(sourceOption, optionIdx));
            }
            else
            {
                // Move from one group to another by deleting, then adding, then moving the option.
                var sourceOption = _dragDropOption;
                ActionQueue.Enqueue(() =>
                {
                    ModManager.OptionEditor.DeleteOption(sourceOption);
                    if (ModManager.OptionEditor.AddOption(group, sourceOption) is { } newOption)
                        ModManager.OptionEditor.MoveOption(newOption, optionIdx);
                });
            }
        }

        _dragDropGroup  = null;
        _dragDropOption = null;
        _draggingAcross = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PrepareStyle()
    {
        var totalWidth = 400f * Im.Style.GlobalScale;
        _buttonSize         = new Vector2(Im.Style.FrameHeight);
        PriorityWidth       = 50 * Im.Style.GlobalScale;
        AvailableWidth      = new Vector2(totalWidth + 3 * _spacing + 2 * _buttonSize.X + PriorityWidth, 0);
        _groupNameWidth     = totalWidth - 3 * (_buttonSize.X + _spacing);
        _spacing            = Im.Style.ItemInnerSpacing.X;
        OptionIdxSelectable = Im.Font.CalculateSize("Option #88."u8);
        _optionNameWidth    = totalWidth - OptionIdxSelectable.X - _buttonSize.X - 2 * _spacing;
        _deleteEnabled      = config.DeleteModModifier.IsActive();
    }
}
