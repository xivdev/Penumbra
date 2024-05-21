using ImGuiNET;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Api.Enums;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab.Groups;

public class AddGroupDrawer : IUiService
{
    private string _groupName = string.Empty;
    private bool _groupNameValid = false;

    private ImcManipulation _imcManip = new(EquipSlot.Head, 1, 1, new ImcEntry());
    private ImcEntry _defaultEntry;
    private bool _imcFileExists;
    private bool _entryExists;
    private bool _entryInvalid;
    private readonly MetaFileManager _metaManager;
    private readonly ModManager _modManager;

    public AddGroupDrawer(MetaFileManager metaManager, ModManager modManager)
    {
        _metaManager = metaManager;
        _modManager = modManager;
        UpdateEntry();
    }

    public void Draw(Mod mod, float width)
    {
        var buttonWidth = new Vector2((width - ImUtf8.ItemInnerSpacing.X) / 2, 0);
        DrawBasicGroups(mod, width, buttonWidth);
        DrawImcData(mod, buttonWidth);
    }

    private void DrawBasicGroups(Mod mod, float width, Vector2 buttonWidth)
    {
        ImGui.SetNextItemWidth(width);
        if (ImUtf8.InputText("##name"u8, ref _groupName, "Enter New Name..."u8))
            _groupNameValid = ModGroupEditor.VerifyFileName(mod, null, _groupName, false);

        DrawSingleGroupButton(mod, buttonWidth);
        ImUtf8.SameLineInner();
        DrawMultiGroupButton(mod, buttonWidth);
    }

    private void DrawSingleGroupButton(Mod mod, Vector2 width)
    {
        if (!ImUtf8.ButtonEx("Add Single Group"u8, _groupNameValid
                    ? "Add a new single selection option group to this mod."u8
                    : "Can not add a new group of this name."u8,
                width, !_groupNameValid))
            return;

        _modManager.OptionEditor.AddModGroup(mod, GroupType.Single, _groupName);
        _groupName = string.Empty;
        _groupNameValid = false;
    }

    private void DrawMultiGroupButton(Mod mod, Vector2 width)
    {
        if (!ImUtf8.ButtonEx("Add Multi Group"u8, _groupNameValid
                    ? "Add a new multi selection option group to this mod."u8
                    : "Can not add a new group of this name."u8,
                width, !_groupNameValid))
            return;

        _modManager.OptionEditor.AddModGroup(mod, GroupType.Multi, _groupName);
        _groupName = string.Empty;
        _groupNameValid = false;
    }

    private void DrawImcInput(float width)
    {
        var change = MetaManipulationDrawer.DrawObjectType(ref _imcManip, width);
        ImUtf8.SameLineInner();
        change |= MetaManipulationDrawer.DrawPrimaryId(ref _imcManip, width);
        if (_imcManip.ObjectType is ObjectType.Weapon or ObjectType.Monster)
        {
            change |= MetaManipulationDrawer.DrawSecondaryId(ref _imcManip, width);
            ImUtf8.SameLineInner();
            change |= MetaManipulationDrawer.DrawVariant(ref _imcManip, width);
        }
        else if (_imcManip.ObjectType is ObjectType.DemiHuman)
        {
            var quarterWidth = (width - ImUtf8.ItemInnerSpacing.X / ImUtf8.GlobalScale) / 2;
            change |= MetaManipulationDrawer.DrawSecondaryId(ref _imcManip, width);
            ImUtf8.SameLineInner();
            change |= MetaManipulationDrawer.DrawSlot(ref _imcManip, quarterWidth);
            ImUtf8.SameLineInner();
            change |= MetaManipulationDrawer.DrawVariant(ref _imcManip, quarterWidth);
        }
        else
        {
            change |= MetaManipulationDrawer.DrawSlot(ref _imcManip, width);
            ImUtf8.SameLineInner();
            change |= MetaManipulationDrawer.DrawVariant(ref _imcManip, width);
        }

        if (change)
            UpdateEntry();
    }

    private void DrawImcData(Mod mod, Vector2 width)
    {
        var halfWidth = width.X / ImUtf8.GlobalScale;
        DrawImcInput(halfWidth);
        DrawImcButton(mod, width);
    }

    private void DrawImcButton(Mod mod, Vector2 width)
    {
        if (ImUtf8.ButtonEx("Add IMC Group"u8, !_groupNameValid
                    ? "Can not add a new group of this name."u8
                    : _entryInvalid
                        ? "The associated IMC entry is invalid."u8
                        : "Add a new multi selection option group to this mod."u8,
                width, !_groupNameValid || _entryInvalid))
        {
            _modManager.OptionEditor.ImcEditor.AddModGroup(mod, _groupName, _imcManip);
            _groupName = string.Empty;
            _groupNameValid = false;
        }

        if (_entryInvalid)
        {
            ImUtf8.SameLineInner();
            var text = _imcFileExists
                ? "IMC Entry Does Not Exist"u8
                : "IMC File Does Not Exist"u8;
            ImUtf8.TextFramed(text, Colors.PressEnterWarningBg, width);
        }
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
            _defaultEntry = new ImcEntry();
            _imcFileExists = false;
            _entryExists = false;
        }

        _imcManip = _imcManip.Copy(_entryExists ? _defaultEntry : new ImcEntry());
        _entryInvalid = !_imcManip.Validate(true);
    }
}
