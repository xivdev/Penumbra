using ImSharp;
using Luna;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;

namespace Penumbra.UI.AdvancedWindow.Meta;

public sealed class EqdpMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<EqdpIdentifier, EqdpEntryInternal>(editor, metaFiles)
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
        Im.Table.NextColumn();
        CopyToClipboardButton("Copy all current EQDP manipulations to clipboard."u8, new Lazy<JToken?>(() => MetaDictionary.SerializeTo([], Editor.Eqdp)));

        Im.Table.NextColumn();
        var validRaceCode = CharacterUtilityData.EqdpIdx(Identifier.GenderRace, false) >= 0;
        var canAdd        = validRaceCode && !Editor.Contains(Identifier);
        var tt = canAdd   ? "Stage this edit."u8 :
            validRaceCode ? "This entry is already edited."u8 : "This combination of race and gender can not be used."u8;
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, tt, !canAdd))
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
        Im.Table.NextColumn();
        var changes = DrawPrimaryId(ref identifier);

        Im.Table.NextColumn();
        changes |= DrawRace(ref identifier);

        Im.Table.NextColumn();
        changes |= DrawGender(ref identifier);

        Im.Table.NextColumn();
        changes |= DrawEquipSlot(ref identifier);
        return changes;
    }

    private static void DrawIdentifier(EqdpIdentifier identifier)
    {
        Im.Table.NextColumn();
        ImEx.TextFramed($"{identifier.SetId.Id}", default, FrameColor);
        Im.Tooltip.OnHover("Model Set ID"u8);

        Im.Table.NextColumn();
        ImEx.TextFramed(identifier.Race.ToNameU8(), default, FrameColor);
        Im.Tooltip.OnHover("Model Race"u8);

        Im.Table.NextColumn();
        ImEx.TextFramed(identifier.Gender.ToNameU8(), default, FrameColor);
        Im.Tooltip.OnHover("Gender"u8);

        Im.Table.NextColumn();
        ImEx.TextFramed(identifier.Slot.ToNameU8(), default, FrameColor);
        Im.Tooltip.OnHover("Equip Slot"u8);
    }

    private static bool DrawEntry(EqdpEntryInternal defaultEntry, ref EqdpEntryInternal entry, bool disabled)
    {
        var       changes = false;
        using var dis     = Im.Disabled(disabled);
        Im.Table.NextColumn();
        if (Checkmark("Material##eqdp"u8, "\0"u8, entry.Material, defaultEntry.Material, out var newMaterial))
        {
            entry   = entry with { Material = newMaterial };
            changes = true;
        }

        Im.Line.Same();
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
        Im.Tooltip.OnHover(
            "Model Set ID - You can usually find this as the 'e####' part of an item path.\nThis should generally not be left <= 1 unless you explicitly want that."u8);
        if (ret)
            identifier = identifier with { SetId = setId };
        return ret;
    }

    public static bool DrawRace(ref EqdpIdentifier identifier, float unscaledWidth = 100)
    {
        var ret = Combos.ModelRace.Draw("##eqdpRace"u8, identifier.Race, "Model Race"u8, unscaledWidth * Im.Style.GlobalScale, out var race);
        if (ret)
            identifier = identifier with { GenderRace = Names.CombinedRace(identifier.Gender, race) };
        return ret;
    }

    public static bool DrawGender(ref EqdpIdentifier identifier, float unscaledWidth = 120)
    {
        var ret = Combos.Gender.Draw("##eqdpGender"u8, identifier.Gender, "Gender"u8, unscaledWidth * Im.Style.GlobalScale, out var gender);
        if (ret)
            identifier = identifier with { GenderRace = Names.CombinedRace(gender, identifier.Race) };
        return ret;
    }

    public static bool DrawEquipSlot(ref EqdpIdentifier identifier, float unscaledWidth = 100)
    {
        var ret = Combos.EqdpEquipSlot.Draw("##eqdpSlot"u8, identifier.Slot, "Equip Slot"u8, unscaledWidth, out var slot);
        if (ret)
            identifier = identifier with { Slot = slot };
        return ret;
    }
}
