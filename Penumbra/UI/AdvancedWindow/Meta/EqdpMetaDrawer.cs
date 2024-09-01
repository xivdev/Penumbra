using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow.Meta;

public sealed class EqdpMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<EqdpIdentifier, EqdpEntryInternal>(editor, metaFiles), IService
{
    public override ReadOnlySpan<byte> Label
        => "Racial Model Edits (EQDP)###EQDP"u8;

    public override int NumColumns
        => 7;

    protected override void Initialize()
    {
        Identifier = new EqdpIdentifier(1, EquipSlot.Head, GenderRace.MidlanderMale);
        UpdateEntry();
    }

    private void UpdateEntry()
        => Entry = new EqdpEntryInternal(ExpandedEqdpFile.GetDefault(MetaFiles, Identifier), Identifier.Slot);

    protected override void DrawNew()
    {
        ImGui.TableNextColumn();
        CopyToClipboardButton("Copy all current EQDP manipulations to clipboard."u8, MetaDictionary.SerializeTo([], Editor.Eqdp));

        ImGui.TableNextColumn();
        var validRaceCode = CharacterUtilityData.EqdpIdx(Identifier.GenderRace, false) >= 0;
        var canAdd        = validRaceCode && !Editor.Contains(Identifier);
        var tt = canAdd   ? "Stage this edit."u8 :
            validRaceCode ? "This entry is already edited."u8 : "This combination of race and gender can not be used."u8;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, tt, disabled: !canAdd))
            Editor.Changes |= Editor.TryAdd(Identifier, Entry);

        if (DrawIdentifierInput(ref Identifier))
            UpdateEntry();

        DrawEntry(Entry, ref Entry, true);
    }

    protected override void DrawEntry(EqdpIdentifier identifier, EqdpEntryInternal entry)
    {
        DrawMetaButtons(identifier, entry);
        DrawIdentifier(identifier);

        var defaultEntry = new EqdpEntryInternal(ExpandedEqdpFile.GetDefault(MetaFiles, identifier), identifier.Slot);
        if (DrawEntry(defaultEntry, ref entry, false))
            Editor.Changes |= Editor.Update(identifier, entry);
    }

    protected override IEnumerable<(EqdpIdentifier, EqdpEntryInternal)> Enumerate()
        => Editor.Eqdp.OrderBy(kvp => kvp.Key.SetId.Id)
            .ThenBy(kvp => kvp.Key.GenderRace)
            .ThenBy(kvp => kvp.Key.Slot)
            .Select(kvp => (kvp.Key, kvp.Value));

    protected override int Count
        => Editor.Eqdp.Count;

    private static bool DrawIdentifierInput(ref EqdpIdentifier identifier)
    {
        ImGui.TableNextColumn();
        var changes = DrawPrimaryId(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawRace(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawGender(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawEquipSlot(ref identifier);
        return changes;
    }

    private static void DrawIdentifier(EqdpIdentifier identifier)
    {
        ImGui.TableNextColumn();
        ImUtf8.TextFramed($"{identifier.SetId.Id}", FrameColor);
        ImUtf8.HoverTooltip("Model Set ID"u8);

        ImGui.TableNextColumn();
        ImUtf8.TextFramed(identifier.Race.ToName(), FrameColor);
        ImUtf8.HoverTooltip("Model Race"u8);

        ImGui.TableNextColumn();
        ImUtf8.TextFramed(identifier.Gender.ToName(), FrameColor);
        ImUtf8.HoverTooltip("Gender"u8);

        ImGui.TableNextColumn();
        ImUtf8.TextFramed(identifier.Slot.ToName(), FrameColor);
        ImUtf8.HoverTooltip("Equip Slot"u8);
    }

    private static bool DrawEntry(EqdpEntryInternal defaultEntry, ref EqdpEntryInternal entry, bool disabled)
    {
        var       changes = false;
        using var dis     = ImRaii.Disabled(disabled);
        ImGui.TableNextColumn();
        if (Checkmark("Material##eqdp"u8, "\0"u8, entry.Material, defaultEntry.Material, out var newMaterial))
        {
            entry   = entry with { Material = newMaterial };
            changes = true;
        }

        ImGui.SameLine();
        if (Checkmark("Model##eqdp"u8, "\0"u8, entry.Model, defaultEntry.Model, out var newModel))
        {
            entry   = entry with { Model = newModel };
            changes = true;
        }

        return changes;
    }

    public static bool DrawPrimaryId(ref EqdpIdentifier identifier, float unscaledWidth = 100)
    {
        var ret = IdInput("##eqdpPrimaryId"u8, unscaledWidth, identifier.SetId.Id, out var setId, 0, ExpandedEqpGmpBase.Count - 1,
            identifier.SetId.Id <= 1);
        ImUtf8.HoverTooltip(
            "Model Set ID - You can usually find this as the 'e####' part of an item path.\nThis should generally not be left <= 1 unless you explicitly want that."u8);
        if (ret)
            identifier = identifier with { SetId = setId };
        return ret;
    }

    public static bool DrawRace(ref EqdpIdentifier identifier, float unscaledWidth = 100)
    {
        var ret = Combos.Race("##eqdpRace", identifier.Race, out var race, unscaledWidth);
        ImUtf8.HoverTooltip("Model Race"u8);
        if (ret)
            identifier = identifier with { GenderRace = Names.CombinedRace(identifier.Gender, race) };
        return ret;
    }

    public static bool DrawGender(ref EqdpIdentifier identifier, float unscaledWidth = 120)
    {
        var ret = Combos.Gender("##eqdpGender", identifier.Gender, out var gender, unscaledWidth);
        ImUtf8.HoverTooltip("Gender"u8);
        if (ret)
            identifier = identifier with { GenderRace = Names.CombinedRace(gender, identifier.Race) };
        return ret;
    }

    public static bool DrawEquipSlot(ref EqdpIdentifier identifier, float unscaledWidth = 100)
    {
        var ret = Combos.EqdpEquipSlot("##eqdpSlot", identifier.Slot, out var slot, unscaledWidth);
        ImUtf8.HoverTooltip("Equip Slot"u8);
        if (ret)
            identifier = identifier with { Slot = slot };
        return ret;
    }
}
