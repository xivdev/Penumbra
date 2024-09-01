using Dalamud.Interface;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow.Meta;

public sealed class EqpMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<EqpIdentifier, EqpEntryInternal>(editor, metaFiles), IService
{
    public override ReadOnlySpan<byte> Label
        => "Equipment Parameter Edits (EQP)###EQP"u8;

    public override int NumColumns
        => 5;

    protected override void Initialize()
    {
        Identifier = new EqpIdentifier(1, EquipSlot.Body);
        UpdateEntry();
    }

    private void UpdateEntry()
        => Entry = new EqpEntryInternal(ExpandedEqpFile.GetDefault(MetaFiles, Identifier.SetId), Identifier.Slot);

    protected override void DrawNew()
    {
        ImGui.TableNextColumn();
        CopyToClipboardButton("Copy all current EQP manipulations to clipboard."u8, MetaDictionary.SerializeTo([], Editor.Eqp));

        ImGui.TableNextColumn();
        var canAdd = !Editor.Contains(Identifier);
        var tt     = canAdd ? "Stage this edit."u8 : "This entry is already edited."u8;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, tt, disabled: !canAdd))
            Editor.Changes |= Editor.TryAdd(Identifier, Entry);

        if (DrawIdentifierInput(ref Identifier))
            UpdateEntry();

        DrawEntry(Identifier.Slot, Entry, ref Entry, true);
    }

    protected override void DrawEntry(EqpIdentifier identifier, EqpEntryInternal entry)
    {
        DrawMetaButtons(identifier, entry);
        DrawIdentifier(identifier);

        var defaultEntry = new EqpEntryInternal(ExpandedEqpFile.GetDefault(MetaFiles, identifier.SetId), identifier.Slot);
        if (DrawEntry(identifier.Slot, defaultEntry, ref entry, false))
            Editor.Changes |= Editor.Update(identifier, entry);
    }

    protected override IEnumerable<(EqpIdentifier, EqpEntryInternal)> Enumerate()
        => Editor.Eqp
            .OrderBy(kvp => kvp.Key.SetId.Id)
            .ThenBy(kvp => kvp.Key.Slot)
            .Select(kvp => (kvp.Key, kvp.Value));

    protected override int Count
        => Editor.Eqp.Count;

    private static bool DrawIdentifierInput(ref EqpIdentifier identifier)
    {
        ImGui.TableNextColumn();
        var changes = DrawPrimaryId(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawEquipSlot(ref identifier);
        return changes;
    }

    private static void DrawIdentifier(EqpIdentifier identifier)
    {
        ImGui.TableNextColumn();
        ImUtf8.TextFramed($"{identifier.SetId.Id}", FrameColor);
        ImUtf8.HoverTooltip("Model Set ID"u8);

        ImGui.TableNextColumn();
        ImUtf8.TextFramed(identifier.Slot.ToName(), FrameColor);
        ImUtf8.HoverTooltip("Equip Slot"u8);
    }

    private static bool DrawEntry(EquipSlot slot, EqpEntryInternal defaultEntry, ref EqpEntryInternal entry, bool disabled)
    {
        var       changes = false;
        using var dis     = ImRaii.Disabled(disabled);
        ImGui.TableNextColumn();
        var offset = Eqp.OffsetAndMask(slot).Item1;
        DrawBox(ref entry, 0);
        for (var i = 1; i < Eqp.EqpAttributes[slot].Count; ++i)
        {
            ImUtf8.SameLineInner();
            DrawBox(ref entry, i);
        }

        return changes;

        void DrawBox(ref EqpEntryInternal entry, int i)
        {
            using var id           = ImUtf8.PushId(i);
            var       flag         = 1u << i;
            var       eqpFlag      = (EqpEntry)((ulong)flag << offset);
            var       defaultValue = (flag & defaultEntry.Value) != 0;
            var       value        = (flag & entry.Value) != 0;
            if (Checkmark("##eqp"u8, eqpFlag.ToLocalName(), value, defaultValue, out var newValue))
            {
                entry   = new EqpEntryInternal(newValue ? entry.Value | flag : entry.Value & ~flag);
                changes = true;
            }
        }
    }

    public static bool DrawPrimaryId(ref EqpIdentifier identifier, float unscaledWidth = 100)
    {
        var ret = IdInput("##eqpPrimaryId"u8, unscaledWidth, identifier.SetId.Id, out var setId, 0, ExpandedEqpGmpBase.Count - 1,
            identifier.SetId.Id <= 1);
        ImUtf8.HoverTooltip(
            "Model Set ID - You can usually find this as the 'e####' part of an item path.\nThis should generally not be left <= 1 unless you explicitly want that."u8);
        if (ret)
            identifier = identifier with { SetId = setId };
        return ret;
    }

    public static bool DrawEquipSlot(ref EqpIdentifier identifier, float unscaledWidth = 100)
    {
        var ret = Combos.EqpEquipSlot("##eqpSlot", identifier.Slot, out var slot, unscaledWidth);
        ImUtf8.HoverTooltip("Equip Slot"u8);
        if (ret)
            identifier = identifier with { Slot = slot };
        return ret;
    }
}
