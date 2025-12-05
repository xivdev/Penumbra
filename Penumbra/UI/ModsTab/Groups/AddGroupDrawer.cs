using ImSharp;
using Penumbra.Api.Enums;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.UI.AdvancedWindow.Meta;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab.Groups;

public class AddGroupDrawer : Luna.IUiService
{
    private string _groupName = string.Empty;
    private bool   _groupNameValid;

    private          ImcIdentifier _imcIdentifier = ImcIdentifier.Default;
    private          ImcEntry      _defaultEntry;
    private          bool          _imcFileExists;
    private          bool          _entryExists;
    private          bool          _entryInvalid;
    private readonly ModManager    _modManager;

    public AddGroupDrawer(ModManager modManager)
    {
        _modManager = modManager;
        UpdateEntry();
    }

    public void Draw(Mod mod, float width)
    {
        var buttonWidth = new Vector2((width - Im.Style.ItemInnerSpacing.X) / 2, 0);
        DrawBasicGroups(mod, width, buttonWidth);
        DrawImcData(mod, buttonWidth);
    }

    private void DrawBasicGroups(Mod mod, float width, Vector2 buttonWidth)
    {
        Im.Item.SetNextWidth(width);
        if (Im.Input.Text("##name"u8, ref _groupName, "Enter New Name..."u8))
            _groupNameValid = ModGroupEditor.VerifyFileName(mod, null, _groupName, false);

        DrawSingleGroupButton(mod, buttonWidth);
        Im.Line.SameInner();
        DrawMultiGroupButton(mod, buttonWidth);
        DrawCombiningGroupButton(mod, buttonWidth);
    }

    private void DrawSingleGroupButton(Mod mod, Vector2 width)
    {
        if (!ImEx.Button("Add Single Group"u8, width, _groupNameValid
                ? "Add a new single selection option group to this mod."u8
                : "Can not add a new group of this name."u8, !_groupNameValid))
            return;

        _modManager.OptionEditor.AddModGroup(mod, GroupType.Single, _groupName);
        _groupName      = string.Empty;
        _groupNameValid = false;
    }

    private void DrawMultiGroupButton(Mod mod, Vector2 width)
    {
        if (!ImEx.Button("Add Multi Group"u8, width, _groupNameValid
                ? "Add a new multi selection option group to this mod."u8
                : "Can not add a new group of this name."u8, !_groupNameValid))
            return;

        _modManager.OptionEditor.AddModGroup(mod, GroupType.Multi, _groupName);
        _groupName      = string.Empty;
        _groupNameValid = false;
    }

    private void DrawCombiningGroupButton(Mod mod, Vector2 width)
    {
        if (!ImEx.Button("Add Combining Group"u8, width, _groupNameValid
                ? "Add a new combining option group to this mod."u8
                : "Can not add a new group of this name."u8, !_groupNameValid))
            return;

        _modManager.OptionEditor.AddModGroup(mod, GroupType.Combining, _groupName);
        _groupName      = string.Empty;
        _groupNameValid = false;
    }

    private void DrawImcInput(float width)
    {
        var change = ImcMetaDrawer.DrawObjectType(ref _imcIdentifier, width);
        Im.Line.SameInner();
        change |= ImcMetaDrawer.DrawPrimaryId(ref _imcIdentifier, width);
        if (_imcIdentifier.ObjectType is ObjectType.Weapon or ObjectType.Monster)
        {
            change |= ImcMetaDrawer.DrawSecondaryId(ref _imcIdentifier, width);
            Im.Line.SameInner();
            change |= ImcMetaDrawer.DrawVariant(ref _imcIdentifier, width);
        }
        else if (_imcIdentifier.ObjectType is ObjectType.DemiHuman)
        {
            var quarterWidth = (width - Im.Style.ItemInnerSpacing.X / Im.Style.GlobalScale) / 2;
            change |= ImcMetaDrawer.DrawSecondaryId(ref _imcIdentifier, width);
            Im.Line.SameInner();
            change |= ImcMetaDrawer.DrawSlot(ref _imcIdentifier, quarterWidth);
            Im.Line.SameInner();
            change |= ImcMetaDrawer.DrawVariant(ref _imcIdentifier, quarterWidth);
        }
        else
        {
            change |= ImcMetaDrawer.DrawSlot(ref _imcIdentifier, width);
            Im.Line.SameInner();
            change |= ImcMetaDrawer.DrawVariant(ref _imcIdentifier, width);
        }

        if (change)
            UpdateEntry();
    }

    private void DrawImcData(Mod mod, Vector2 width)
    {
        var halfWidth = width.X / Im.Style.GlobalScale;
        DrawImcInput(halfWidth);
        DrawImcButton(mod, width);
    }

    private void DrawImcButton(Mod mod, Vector2 width)
    {
        if (ImEx.Button("Add IMC Group"u8, width, !_groupNameValid
                ? "Can not add a new group of this name."u8
                : _entryInvalid
                    ? "The associated IMC entry is invalid."u8
                    : "Add a new multi selection option group to this mod."u8, !_groupNameValid || _entryInvalid))
        {
            _modManager.OptionEditor.ImcEditor.AddModGroup(mod, _groupName, _imcIdentifier, _defaultEntry);
            _groupName      = string.Empty;
            _groupNameValid = false;
        }

        if (_entryInvalid)
        {
            Im.Line.SameInner();
            var text = _imcFileExists
                ? "IMC Entry Does Not Exist"u8
                : "IMC File Does Not Exist"u8;
            ImEx.TextFramed(text, width, Colors.PressEnterWarningBg);
        }
    }

    private void UpdateEntry()
    {
        (_defaultEntry, _imcFileExists, _entryExists) = ImcChecker.GetDefaultEntry(_imcIdentifier, false);
        _entryInvalid                                 = !_imcIdentifier.Validate() || _defaultEntry.MaterialId == 0 || !_entryExists;
    }
}
