using Dalamud.Interface;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Utility;
using ImGuiNET;
using Lumina.Data.Files;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using OtterGui.Text.EndObjects;
using Penumbra.Api.Enums;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using Penumbra.UI.Classes;
using ImcFile = Penumbra.Meta.Files.ImcFile;

namespace Penumbra.UI.ModsTab;

public static class MetaManipulationDrawer
{
    public static bool DrawObjectType(ref ImcManipulation manip, float width = 110)
    {
        var ret = Combos.ImcType("##imcType", manip.ObjectType, out var type, width);
        ImUtf8.HoverTooltip("Object Type"u8);

        if (ret)
        {
            var equipSlot = type switch
            {
                ObjectType.Equipment => manip.EquipSlot.IsEquipment() ? manip.EquipSlot : EquipSlot.Head,
                ObjectType.DemiHuman => manip.EquipSlot.IsEquipment() ? manip.EquipSlot : EquipSlot.Head,
                ObjectType.Accessory => manip.EquipSlot.IsAccessory() ? manip.EquipSlot : EquipSlot.Ears,
                _                    => EquipSlot.Unknown,
            };
            manip = new ImcManipulation(type, manip.BodySlot, manip.PrimaryId, manip.SecondaryId == 0 ? 1 : manip.SecondaryId,
                manip.Variant.Id, equipSlot, manip.Entry);
        }

        return ret;
    }

    public static bool DrawPrimaryId(ref ImcManipulation manip, float unscaledWidth = 80)
    {
        var ret = IdInput("##imcPrimaryId"u8, unscaledWidth, manip.PrimaryId.Id, out var newId, 0, ushort.MaxValue,
            manip.PrimaryId.Id <= 1);
        ImUtf8.HoverTooltip("Primary ID - You can usually find this as the 'x####' part of an item path.\n"u8
          + "This should generally not be left <= 1 unless you explicitly want that."u8);
        if (ret)
            manip = new ImcManipulation(manip.ObjectType, manip.BodySlot, newId, manip.SecondaryId, manip.Variant.Id, manip.EquipSlot,
                manip.Entry);
        return ret;
    }

    public static bool DrawSecondaryId(ref ImcManipulation manip, float unscaledWidth = 100)
    {
        var ret = IdInput("##imcSecondaryId"u8, unscaledWidth, manip.SecondaryId.Id, out var newId, 0, ushort.MaxValue, false);
        ImUtf8.HoverTooltip("Secondary ID"u8);
        if (ret)
            manip = new ImcManipulation(manip.ObjectType, manip.BodySlot, manip.PrimaryId, newId, manip.Variant.Id, manip.EquipSlot,
                manip.Entry);
        return ret;
    }

    public static bool DrawVariant(ref ImcManipulation manip, float unscaledWidth = 45)
    {
        var ret = IdInput("##imcVariant"u8, unscaledWidth, manip.Variant.Id, out var newId, 0, byte.MaxValue, false);
        ImUtf8.HoverTooltip("Variant ID"u8);
        if (ret)
            manip = new ImcManipulation(manip.ObjectType, manip.BodySlot, manip.PrimaryId, manip.SecondaryId, (byte)newId, manip.EquipSlot,
                manip.Entry);
        return ret;
    }

    public static bool DrawSlot(ref ImcManipulation manip, float unscaledWidth = 100)
    {
        bool      ret;
        EquipSlot slot;
        switch (manip.ObjectType)
        {
            case ObjectType.Equipment:
            case ObjectType.DemiHuman:
                ret = Combos.EqpEquipSlot("##slot", manip.EquipSlot, out slot, unscaledWidth);
                break;
            case ObjectType.Accessory:
                ret = Combos.AccessorySlot("##slot", manip.EquipSlot, out slot, unscaledWidth);
                break;
            default: return false;
        }

        ImUtf8.HoverTooltip("Equip Slot"u8);
        if (ret)
            manip = new ImcManipulation(manip.ObjectType, manip.BodySlot, manip.PrimaryId, manip.SecondaryId, manip.Variant.Id, slot,
                manip.Entry);
        return ret;
    }

    // A number input for ids with a optional max id of given width.
    // Returns true if newId changed against currentId.
    private static bool IdInput(ReadOnlySpan<byte> label, float unscaledWidth, ushort currentId, out ushort newId, int minId, int maxId,
        bool border)
    {
        int tmp = currentId;
        ImGui.SetNextItemWidth(unscaledWidth * ImUtf8.GlobalScale);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, UiHelpers.Scale, border);
        using var color = ImRaii.PushColor(ImGuiCol.Border, Colors.RegexWarningBorder, border);
        if (ImUtf8.InputScalar(label, ref tmp))
            tmp = Math.Clamp(tmp, minId, maxId);

        newId = (ushort)tmp;
        return newId != currentId;
    }
}

public class AddGroupDrawer : IUiService
{
    private string _groupName      = string.Empty;
    private bool   _groupNameValid = false;

    private          ImcManipulation _imcManip = new(EquipSlot.Head, 1, 1, new ImcEntry());
    private          ImcEntry        _defaultEntry;
    private          bool            _imcFileExists;
    private          bool            _entryExists;
    private          bool            _entryInvalid;
    private readonly MetaFileManager _metaManager;
    private readonly ModManager      _modManager;

    public AddGroupDrawer(MetaFileManager metaManager, ModManager modManager)
    {
        _metaManager = metaManager;
        _modManager  = modManager;
        UpdateEntry();
    }

    public void Draw(Mod mod, float width)
    {
        DrawBasicGroups(mod, width);
        DrawImcData(mod, width);
    }

    private void UpdateEntry()
    {
        try
        {
            _defaultEntry = ImcFile.GetDefault(_metaManager, _imcManip.GamePath(), _imcManip.EquipSlot, _imcManip.Variant,
                out _entryExists);
            _imcFileExists = true;
        }
        catch (Exception)
        {
            _defaultEntry  = new ImcEntry();
            _imcFileExists = false;
            _entryExists   = false;
        }

        _imcManip     = _imcManip.Copy(_entryExists ? _defaultEntry : new ImcEntry());
        _entryInvalid = !_imcManip.Validate(true);
    }


    private void DrawBasicGroups(Mod mod, float width)
    {
        ImGui.SetNextItemWidth(width);
        if (ImUtf8.InputText("##name"u8, ref _groupName, "Enter New Name..."u8))
            _groupNameValid = ModGroupEditor.VerifyFileName(mod, null, _groupName, false);

        var buttonWidth = new Vector2((width - ImUtf8.ItemInnerSpacing.X) / 2, 0);
        if (ImUtf8.ButtonEx("Add Single Group"u8, _groupNameValid
                    ? "Add a new single selection option group to this mod."u8
                    : "Can not add a new group of this name."u8,
                buttonWidth, !_groupNameValid))
        {
            _modManager.OptionEditor.AddModGroup(mod, GroupType.Single, _groupName);
            _groupName      = string.Empty;
            _groupNameValid = false;
        }

        ImUtf8.SameLineInner();
        if (ImUtf8.ButtonEx("Add Multi Group"u8, _groupNameValid
                    ? "Add a new multi selection option group to this mod."u8
                    : "Can not add a new group of this name."u8,
                buttonWidth, !_groupNameValid))
        {
            _modManager.OptionEditor.AddModGroup(mod, GroupType.Multi, _groupName);
            _groupName      = string.Empty;
            _groupNameValid = false;
        }
    }

    private void DrawImcData(Mod mod, float width)
    {
        var halfWidth = (width - ImUtf8.ItemInnerSpacing.X) / 2 / ImUtf8.GlobalScale;
        var change    = MetaManipulationDrawer.DrawObjectType(ref _imcManip, halfWidth);
        ImUtf8.SameLineInner();
        change |= MetaManipulationDrawer.DrawPrimaryId(ref _imcManip, halfWidth);
        if (_imcManip.ObjectType is ObjectType.Weapon or ObjectType.Monster)
        {
            change |= MetaManipulationDrawer.DrawSecondaryId(ref _imcManip, halfWidth);
            ImUtf8.SameLineInner();
            change |= MetaManipulationDrawer.DrawVariant(ref _imcManip, halfWidth);
        }
        else if (_imcManip.ObjectType is ObjectType.DemiHuman)
        {
            var quarterWidth = (halfWidth - ImUtf8.ItemInnerSpacing.X / ImUtf8.GlobalScale) / 2;
            change |= MetaManipulationDrawer.DrawSecondaryId(ref _imcManip, halfWidth);
            ImUtf8.SameLineInner();
            change |= MetaManipulationDrawer.DrawSlot(ref _imcManip, quarterWidth);
            ImUtf8.SameLineInner();
            change |= MetaManipulationDrawer.DrawVariant(ref _imcManip, quarterWidth);
        }
        else
        {
            change |= MetaManipulationDrawer.DrawSlot(ref _imcManip, halfWidth);
            ImUtf8.SameLineInner();
            change |= MetaManipulationDrawer.DrawVariant(ref _imcManip, halfWidth);
        }

        if (change)
            UpdateEntry();

        var buttonWidth = new Vector2(halfWidth * ImUtf8.GlobalScale, 0);

        if (ImUtf8.ButtonEx("Add IMC Group"u8, !_groupNameValid
                    ? "Can not add a new group of this name."u8
                    : _entryInvalid ?
                        "The associated IMC entry is invalid."u8
                        : "Add a new multi selection option group to this mod."u8,
                buttonWidth, !_groupNameValid || _entryInvalid))
        {
            _modManager.OptionEditor.ImcEditor.AddModGroup(mod, _groupName, _imcManip);
            _groupName      = string.Empty;
            _groupNameValid = false;
        }

        if (_entryInvalid)
        {
            ImUtf8.SameLineInner();
            var text = _imcFileExists
                ? "IMC Entry Does Not Exist"
                : "IMC File Does Not Exist";
            ImGuiUtil.DrawTextButton(text, buttonWidth, Colors.PressEnterWarningBg);
        }
    }
}

public sealed class ModGroupEditDrawer(
    ModManager modManager,
    Configuration config,
    FilenameService filenames,
    DescriptionEditPopup descriptionPopup) : IUiService
{
    private static ReadOnlySpan<byte> DragDropLabel
        => "##DragOption"u8;

    private Vector2 _buttonSize;
    private Vector2 _availableWidth;
    private float   _priorityWidth;
    private float   _groupNameWidth;
    private float   _optionNameWidth;
    private float   _spacing;
    private Vector2 _optionIdxSelectable;
    private bool    _deleteEnabled;

    private string?      _currentGroupName;
    private ModPriority? _currentGroupPriority;
    private IModGroup?   _currentGroupEdited;
    private bool         _isGroupNameValid = true;

    private          string?       _newOptionName;
    private          IModGroup?    _newOptionGroup;
    private readonly Queue<Action> _actionQueue = new();

    private IModGroup?  _dragDropGroup;
    private IModOption? _dragDropOption;

    public void Draw(Mod mod)
    {
        PrepareStyle();

        using var id = ImUtf8.PushId("##GroupEdit"u8);
        foreach (var (group, groupIdx) in mod.Groups.WithIndex())
            DrawGroup(group, groupIdx);

        while (_actionQueue.TryDequeue(out var action))
            action.Invoke();
    }

    private void DrawGroup(IModGroup group, int idx)
    {
        using var id    = ImUtf8.PushId(idx);
        using var frame = ImRaii.FramedGroup($"Group #{idx + 1}");
        DrawGroupNameRow(group, idx);
        switch (group)
        {
            case SingleModGroup s:
                DrawSingleGroup(s);
                break;
            case MultiModGroup m:
                DrawMultiGroup(m);
                break;
            case ImcModGroup i:
                DrawImcGroup(i);
                break;
        }
    }

    private void DrawGroupNameRow(IModGroup group, int idx)
    {
        DrawGroupName(group);
        ImUtf8.SameLineInner();
        DrawGroupMoveButtons(group, idx);
        ImUtf8.SameLineInner();
        DrawGroupOpenFile(group, idx);
        ImUtf8.SameLineInner();
        DrawGroupDescription(group);
        ImUtf8.SameLineInner();
        DrawGroupDelete(group);
        ImUtf8.SameLineInner();
        DrawGroupPriority(group);
    }

    private void DrawGroupName(IModGroup group)
    {
        var text = _currentGroupEdited == group ? _currentGroupName ?? group.Name : group.Name;
        ImGui.SetNextItemWidth(_groupNameWidth);
        using var border = ImRaii.PushFrameBorder(UiHelpers.ScaleX2, Colors.RegexWarningBorder, !_isGroupNameValid);
        if (ImUtf8.InputText("##GroupName"u8, ref text))
        {
            _currentGroupEdited = group;
            _currentGroupName   = text;
            _isGroupNameValid   = text == group.Name || ModGroupEditor.VerifyFileName(group.Mod, group, text, false);
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
            ? "Change the Group name."u8
            : "Current name can not be used for this group."u8;
        ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, tt);
    }

    private void DrawGroupDelete(IModGroup group)
    {
        if (ImUtf8.IconButton(FontAwesomeIcon.Trash, !_deleteEnabled))
            _actionQueue.Enqueue(() => modManager.OptionEditor.DeleteModGroup(group));

        if (_deleteEnabled)
            ImUtf8.HoverTooltip("Delete this option group."u8);
        else
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled,
                $"Delete this option group.\nHold {config.DeleteModModifier} while clicking to delete.");
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawGroupDescription(IModGroup group)
    {
        if (ImUtf8.IconButton(FontAwesomeIcon.Edit, "Edit group description."u8))
            descriptionPopup.Open(group);
    }

    private void DrawGroupMoveButtons(IModGroup group, int idx)
    {
        var isFirst = idx == 0;
        if (ImUtf8.IconButton(FontAwesomeIcon.ArrowUp, isFirst))
            _actionQueue.Enqueue(() => modManager.OptionEditor.MoveModGroup(group, idx - 1));

        if (isFirst)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, "Can not move this group further upwards."u8);
        else
            ImUtf8.HoverTooltip($"Move this group up to group {idx}.");


        ImUtf8.SameLineInner();
        var isLast = idx == group.Mod.Groups.Count - 1;
        if (ImUtf8.IconButton(FontAwesomeIcon.ArrowDown, isLast))
            _actionQueue.Enqueue(() => modManager.OptionEditor.MoveModGroup(group, idx + 1));

        if (isLast)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, "Can not move this group further downwards."u8);
        else
            ImUtf8.HoverTooltip($"Move this group down to group {idx + 2}.");
    }

    private void DrawGroupOpenFile(IModGroup group, int idx)
    {
        var fileName   = filenames.OptionGroupFile(group.Mod, idx, config.ReplaceNonAsciiOnImport);
        var fileExists = File.Exists(fileName);
        if (ImUtf8.IconButton(FontAwesomeIcon.FileExport, !fileExists))
            try
            {
                Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
            }
            catch (Exception e)
            {
                Penumbra.Messager.NotificationMessage(e, "Could not open editor.", NotificationType.Error);
            }

        if (fileExists)
            ImUtf8.HoverTooltip($"Open the {group.Name} json file in the text editor of your choice.");
        else
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"The {group.Name} json file does not exist.");
    }

    private void DrawSingleGroup(SingleModGroup group)
    {
        foreach (var (option, optionIdx) in group.OptionData.WithIndex())
        {
            using var id = ImRaii.PushId(optionIdx);
            DrawOptionPosition(group, option, optionIdx);

            ImUtf8.SameLineInner();
            DrawOptionDefaultSingleBehaviour(group, option, optionIdx);

            ImUtf8.SameLineInner();
            DrawOptionName(option);

            ImUtf8.SameLineInner();
            DrawOptionDescription(option);

            ImUtf8.SameLineInner();
            DrawOptionDelete(option);

            ImUtf8.SameLineInner();
            ImGui.Dummy(new Vector2(_priorityWidth, 0));
        }

        DrawNewOption(group);
        var convertible = group.Options.Count <= IModGroup.MaxMultiOptions;
        if (ImUtf8.ButtonEx("Convert to Multi Group", _availableWidth, !convertible))
            _actionQueue.Enqueue(() => modManager.OptionEditor.SingleEditor.ChangeToMulti(group));
        if (!convertible)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled,
                "Can not convert to multi group since maximum number of options is exceeded."u8);
    }

    private void DrawMultiGroup(MultiModGroup group)
    {
        foreach (var (option, optionIdx) in group.OptionData.WithIndex())
        {
            using var id = ImRaii.PushId(optionIdx);
            DrawOptionPosition(group, option, optionIdx);

            ImUtf8.SameLineInner();
            DrawOptionDefaultMultiBehaviour(group, option, optionIdx);

            ImUtf8.SameLineInner();
            DrawOptionName(option);

            ImUtf8.SameLineInner();
            DrawOptionDescription(option);

            ImUtf8.SameLineInner();
            DrawOptionDelete(option);

            ImUtf8.SameLineInner();
            DrawOptionPriority(option);
        }

        DrawNewOption(group);
        if (ImUtf8.Button("Convert to Single Group"u8, _availableWidth))
            _actionQueue.Enqueue(() => modManager.OptionEditor.MultiEditor.ChangeToSingle(group));
    }

    private void DrawImcGroup(ImcModGroup group)
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawOptionPosition(IModGroup group, IModOption option, int optionIdx)
    {
        ImGui.AlignTextToFramePadding();
        ImUtf8.Selectable($"Option #{optionIdx + 1}", false, size: _optionIdxSelectable);
        Target(group, optionIdx);
        Source(option);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawOptionDefaultSingleBehaviour(IModGroup group, IModOption option, int optionIdx)
    {
        var isDefaultOption = group.DefaultSettings.AsIndex == optionIdx;
        if (ImUtf8.RadioButton("##default"u8, isDefaultOption))
            modManager.OptionEditor.ChangeModGroupDefaultOption(group, Setting.Single(optionIdx));
        ImUtf8.HoverTooltip($"Set {option.Name} as the default choice for this group.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawOptionDefaultMultiBehaviour(IModGroup group, IModOption option, int optionIdx)
    {
        var isDefaultOption = group.DefaultSettings.HasFlag(optionIdx);
        if (ImUtf8.Checkbox("##default"u8, ref isDefaultOption))
            modManager.OptionEditor.ChangeModGroupDefaultOption(group, group.DefaultSettings.SetBit(optionIdx, isDefaultOption));
        ImUtf8.HoverTooltip($"{(isDefaultOption ? "Disable"u8 : "Enable"u8)} {option.Name} per default in this group.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawOptionDescription(IModOption option)
    {
        if (ImUtf8.IconButton(FontAwesomeIcon.Edit, "Edit option description."u8))
            descriptionPopup.Open(option);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawOptionPriority(MultiSubMod option)
    {
        var priority = option.Priority.Value;
        ImGui.SetNextItemWidth(_priorityWidth);
        if (ImUtf8.InputScalarOnDeactivated("##Priority"u8, ref priority))
            modManager.OptionEditor.MultiEditor.ChangeOptionPriority(option, new ModPriority(priority));
        ImUtf8.HoverTooltip("Option priority inside the mod."u8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawOptionName(IModOption option)
    {
        var name = option.Name;
        ImGui.SetNextItemWidth(_optionNameWidth);
        if (ImUtf8.InputTextOnDeactivated("##Name"u8, ref name))
            modManager.OptionEditor.RenameOption(option, name);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawOptionDelete(IModOption option)
    {
        if (ImUtf8.IconButton(FontAwesomeIcon.Trash, !_deleteEnabled))
            _actionQueue.Enqueue(() => modManager.OptionEditor.DeleteOption(option));

        if (_deleteEnabled)
            ImUtf8.HoverTooltip("Delete this option."u8);
        else
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled,
                $"Delete this option.\nHold {config.DeleteModModifier} while clicking to delete.");
    }

    private void DrawNewOption(SingleModGroup group)
    {
        var count = group.Options.Count;
        if (count >= int.MaxValue)
            return;

        DrawNewOptionBase(group, count);

        var validName = _newOptionName?.Length > 0;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, validName
                ? "Add a new option to this group."u8
                : "Please enter a name for the new option."u8, !validName))
        {
            modManager.OptionEditor.SingleEditor.AddOption(group, _newOptionName!);
            _newOptionName = null;
        }
    }

    private void DrawNewOption(MultiModGroup group)
    {
        var count = group.Options.Count;
        if (count >= IModGroup.MaxMultiOptions)
            return;

        DrawNewOptionBase(group, count);

        var validName = _newOptionName?.Length > 0;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, validName
                ? "Add a new option to this group."u8
                : "Please enter a name for the new option."u8, !validName))
        {
            modManager.OptionEditor.MultiEditor.AddOption(group, _newOptionName!);
            _newOptionName = null;
        }
    }

    private void DrawNewOption(ImcModGroup group)
    {
        // TODO
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawNewOptionBase(IModGroup group, int count)
    {
        ImUtf8.Selectable($"Option #{count + 1}", false, size: _optionIdxSelectable);
        Target(group, count);

        ImUtf8.SameLineInner();
        ImUtf8.IconDummy();

        ImUtf8.SameLineInner();
        ImGui.SetNextItemWidth(_optionNameWidth);
        var newName = _newOptionGroup == group
            ? _newOptionName ?? string.Empty
            : string.Empty;
        if (ImUtf8.InputText("##newOption"u8, ref newName, "Add new option..."u8))
        {
            _newOptionName  = newName;
            _newOptionGroup = group;
        }

        ImUtf8.SameLineInner();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Source(IModOption option)
    {
        if (option.Group is not ITexToolsGroup)
            return;

        using var source = ImUtf8.DragDropSource();
        if (!source)
            return;

        if (!DragDropSource.SetPayload(DragDropLabel))
        {
            _dragDropGroup  = option.Group;
            _dragDropOption = option;
        }

        ImGui.TextUnformatted($"Dragging option {option.Name} from group {option.Group.Name}...");
    }

    private void Target(IModGroup group, int optionIdx)
    {
        if (group is not ITexToolsGroup)
            return;

        if (_dragDropGroup != group && _dragDropGroup != null && group is MultiModGroup { Options.Count: >= IModGroup.MaxMultiOptions })
            return;

        using var target = ImRaii.DragDropTarget();
        if (!target.Success || !DragDropTarget.CheckPayload(DragDropLabel))
            return;

        if (_dragDropGroup != null && _dragDropOption != null)
        {
            if (_dragDropGroup == group)
            {
                var sourceOption = _dragDropOption;
                _actionQueue.Enqueue(() => modManager.OptionEditor.MoveOption(sourceOption, optionIdx));
            }
            else
            {
                // Move from one group to another by deleting, then adding, then moving the option.
                var sourceOption = _dragDropOption;
                _actionQueue.Enqueue(() =>
                {
                    modManager.OptionEditor.DeleteOption(sourceOption);
                    if (modManager.OptionEditor.AddOption(group, sourceOption) is { } newOption)
                        modManager.OptionEditor.MoveOption(newOption, optionIdx);
                });
            }
        }

        _dragDropGroup  = null;
        _dragDropOption = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PrepareStyle()
    {
        var totalWidth = 400f * ImUtf8.GlobalScale;
        _buttonSize          = new Vector2(ImUtf8.FrameHeight);
        _priorityWidth       = 50 * ImUtf8.GlobalScale;
        _availableWidth      = new Vector2(totalWidth + 3 * _spacing + 2 * _buttonSize.X + _priorityWidth, 0);
        _groupNameWidth      = totalWidth - 3 * (_buttonSize.X + _spacing);
        _spacing             = ImGui.GetStyle().ItemInnerSpacing.X;
        _optionIdxSelectable = ImUtf8.CalcTextSize("Option #88."u8);
        _optionNameWidth     = totalWidth - _optionIdxSelectable.X - _buttonSize.X - 2 * _spacing;
        _deleteEnabled       = config.DeleteModModifier.IsActive();
    }
}
