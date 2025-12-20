using ImSharp;
using Luna;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Enums;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;

namespace Penumbra.UI.AdvancedWindow.Meta;

public sealed class EstMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<EstIdentifier, EstEntry>(editor, metaFiles)
{
    public override ReadOnlySpan<byte> Label
        => "Extra Skeleton Parameters (EST)###EST"u8;

    public override int NumColumns
        => 7;

    protected override void Initialize()
    {
        Identifier = new EstIdentifier(1, EstType.Hair, GenderRace.MidlanderMale);
        UpdateEntry();
    }

    private void UpdateEntry()
        => Entry = EstFile.GetDefault(MetaFiles, Identifier.Slot, Identifier.GenderRace, Identifier.SetId);

    protected override void DrawNew()
    {
        Im.Table.NextColumn();
        CopyToClipboardButton("Copy all current EST manipulations to clipboard."u8,
            new Lazy<JToken?>(() => MetaDictionary.SerializeTo([], Editor.Est)));

        Im.Table.NextColumn();
        var canAdd = !Editor.Contains(Identifier);
        var tt     = canAdd ? "Stage this edit."u8 : "This entry is already edited."u8;
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, tt, !canAdd))
            Editor.Changes |= Editor.TryAdd(Identifier, Entry);

        if (DrawIdentifierInput(ref Identifier))
            UpdateEntry();

        DrawEntry(Entry, ref Entry, true);
    }

    protected override void DrawEntry(EstIdentifier identifier, EstEntry entry)
    {
        DrawMetaButtons(identifier, entry);
        DrawIdentifier(identifier);

        var defaultEntry = EstFile.GetDefault(MetaFiles, identifier.Slot, identifier.GenderRace, identifier.SetId);
        if (DrawEntry(defaultEntry, ref entry, false))
            Editor.Changes |= Editor.Update(identifier, entry);
    }

    protected override IEnumerable<(EstIdentifier, EstEntry)> Enumerate()
        => Editor.Est
            .OrderBy(kvp => kvp.Key.SetId.Id)
            .ThenBy(kvp => kvp.Key.GenderRace)
            .ThenBy(kvp => kvp.Key.Slot)
            .Select(kvp => (kvp.Key, kvp.Value));

    protected override int Count
        => Editor.Est.Count;

    private static bool DrawIdentifierInput(ref EstIdentifier identifier)
    {
        Im.Table.NextColumn();
        var changes = DrawPrimaryId(ref identifier);

        Im.Table.NextColumn();
        changes |= DrawRace(ref identifier);

        Im.Table.NextColumn();
        changes |= DrawGender(ref identifier);

        Im.Table.NextColumn();
        changes |= DrawSlot(ref identifier);

        return changes;
    }

    private static void DrawIdentifier(EstIdentifier identifier)
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
        Im.Tooltip.OnHover("Extra Skeleton Type"u8);
    }

    private static bool DrawEntry(EstEntry defaultEntry, ref EstEntry entry, bool disabled)
    {
        using var dis = Im.Disabled(disabled);
        Im.Table.NextColumn();
        var ret = DragInput("##estValue"u8, [],    100f * Im.Style.GlobalScale, entry.Value, defaultEntry.Value, out var newValue, (ushort)0,
            ushort.MaxValue,                0.05f, !disabled);
        if (ret)
            entry = new EstEntry(newValue);
        return ret;
    }

    public static bool DrawPrimaryId(ref EstIdentifier identifier, float unscaledWidth = 100)
    {
        var ret = IdInput("##estPrimaryId"u8, unscaledWidth, identifier.SetId.Id, out var setId, 0, ExpandedEqpGmpBase.Count - 1,
            identifier.SetId.Id <= 1);
        Im.Tooltip.OnHover(
            "Model Set ID - You can usually find this as the 'e####' part of an item path.\nThis should generally not be left <= 1 unless you explicitly want that."u8);
        if (ret)
            identifier = identifier with { SetId = setId };
        return ret;
    }

    public static bool DrawRace(ref EstIdentifier identifier, float unscaledWidth = 100)
    {
        var ret = Combos.ModelRace.Draw("##estRace"u8, identifier.Race, "Model Race"u8, unscaledWidth * Im.Style.GlobalScale, out var race);
        if (ret)
            identifier = identifier with { GenderRace = Names.CombinedRace(identifier.Gender, race) };
        return ret;
    }

    public static bool DrawGender(ref EstIdentifier identifier, float unscaledWidth = 120)
    {
        var ret = Combos.Gender.Draw("##estGender"u8, identifier.Gender, "Gender"u8, unscaledWidth * Im.Style.GlobalScale, out var gender);
        if (ret)
            identifier = identifier with { GenderRace = Names.CombinedRace(gender, identifier.Race) };
        return ret;
    }

    public static bool DrawSlot(ref EstIdentifier identifier, float unscaledWidth = 200)
    {
        var ret = Combos.EstSlot.Draw("##estSlot"u8, identifier.Slot, "Extra Skeleton Type"u8, unscaledWidth * Im.Style.GlobalScale, out var slot);
        if (ret)
            identifier = identifier with { Slot = slot };
        return ret;
    }
}
