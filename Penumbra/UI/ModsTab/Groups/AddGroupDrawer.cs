using ImGuiNET;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Api.Enums;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab.Groups;

public class AddGroupDrawer : IUiService
{
    private string _groupName = string.Empty;
    private bool   _groupNameValid;

    private          ImcIdentifier _imcIdentifier = ImcIdentifier.Default;
    private          ImcEntry      _defaultEntry;
    private          bool          _imcFileExists;
    private          bool          _entryExists;
    private          bool          _entryInvalid;
    private readonly ImcChecker    _imcChecker;
    private readonly ModManager    _modManager;

    public AddGroupDrawer(ModManager modManager, ImcChecker imcChecker)
    {
        _modManager = modManager;
        _imcChecker = imcChecker;
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
        _groupName      = string.Empty;
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
        _groupName      = string.Empty;
        _groupNameValid = false;
    }

    private void DrawImcInput(float width)
    {
        var change = ImcManipulationDrawer.DrawObjectType(ref _imcIdentifier, width);
        ImUtf8.SameLineInner();
        change |= ImcManipulationDrawer.DrawPrimaryId(ref _imcIdentifier, width);
        if (_imcIdentifier.ObjectType is ObjectType.Weapon or ObjectType.Monster)
        {
            change |= ImcManipulationDrawer.DrawSecondaryId(ref _imcIdentifier, width);
            ImUtf8.SameLineInner();
            change |= ImcManipulationDrawer.DrawVariant(ref _imcIdentifier, width);
        }
        else if (_imcIdentifier.ObjectType is ObjectType.DemiHuman)
        {
            var quarterWidth = (width - ImUtf8.ItemInnerSpacing.X / ImUtf8.GlobalScale) / 2;
            change |= ImcManipulationDrawer.DrawSecondaryId(ref _imcIdentifier, width);
            ImUtf8.SameLineInner();
            change |= ImcManipulationDrawer.DrawSlot(ref _imcIdentifier, quarterWidth);
            ImUtf8.SameLineInner();
            change |= ImcManipulationDrawer.DrawVariant(ref _imcIdentifier, quarterWidth);
        }
        else
        {
            change |= ImcManipulationDrawer.DrawSlot(ref _imcIdentifier, width);
            ImUtf8.SameLineInner();
            change |= ImcManipulationDrawer.DrawVariant(ref _imcIdentifier, width);
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
            _modManager.OptionEditor.ImcEditor.AddModGroup(mod, _groupName, _imcIdentifier, _defaultEntry);
            _groupName      = string.Empty;
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
        (_defaultEntry, _imcFileExists, _entryExists) = _imcChecker.GetDefaultEntry(_imcIdentifier, false);
        _entryInvalid                                 = !_imcIdentifier.Validate() || _defaultEntry.MaterialId == 0 || !_entryExists;
    }
}
