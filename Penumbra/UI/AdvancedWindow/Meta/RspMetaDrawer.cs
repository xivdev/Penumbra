using ImSharp;
using Luna;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Enums;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;

namespace Penumbra.UI.AdvancedWindow.Meta;

public sealed class RspMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<RspIdentifier, RspEntry>(editor, metaFiles)
{
    public override ReadOnlySpan<byte> Label
        => "Racial Scaling Edits (RSP)###RSP"u8;

    public override int NumColumns
        => 5;

    protected override void Initialize()
    {
        Identifier = new RspIdentifier(SubRace.Midlander, RspAttribute.MaleMinSize);
        UpdateEntry();
    }

    private void UpdateEntry()
        => Entry = CmpFile.GetDefault(MetaFiles, Identifier.SubRace, Identifier.Attribute);

    protected override void DrawNew()
    {
        Im.Table.NextColumn();
        CopyToClipboardButton("Copy all current RSP manipulations to clipboard."u8,
            new Lazy<JToken?>(() => MetaDictionary.SerializeTo([], Editor.Rsp)));

        Im.Table.NextColumn();
        var canAdd = !Editor.Contains(Identifier);
        var tt     = canAdd ? "Stage this edit."u8 : "This entry is already edited."u8;
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, tt, !canAdd))
            Editor.Changes |= Editor.TryAdd(Identifier, Entry);

        if (DrawIdentifierInput(ref Identifier))
            UpdateEntry();

        DrawEntry(Entry, ref Entry, true);
    }

    protected override void DrawEntry(RspIdentifier identifier, RspEntry entry)
    {
        DrawMetaButtons(identifier, entry);
        DrawIdentifier(identifier);

        var defaultEntry = CmpFile.GetDefault(MetaFiles, identifier.SubRace, identifier.Attribute);
        if (DrawEntry(defaultEntry, ref entry, false))
            Editor.Changes |= Editor.Update(identifier, entry);
    }

    protected override IEnumerable<(RspIdentifier, RspEntry)> Enumerate()
        => Editor.Rsp
            .OrderBy(kvp => kvp.Key.SubRace)
            .ThenBy(kvp => kvp.Key.Attribute)
            .Select(kvp => (kvp.Key, kvp.Value));

    protected override int Count
        => Editor.Rsp.Count;

    private static bool DrawIdentifierInput(ref RspIdentifier identifier)
    {
        Im.Table.NextColumn();
        var changes = DrawSubRace(ref identifier);

        Im.Table.NextColumn();
        changes |= DrawAttribute(ref identifier);
        return changes;
    }

    private static void DrawIdentifier(RspIdentifier identifier)
    {
        Im.Table.NextColumn();
        ImEx.TextFramed(identifier.SubRace.ToNameU8(), default, FrameColor);
        Im.Tooltip.OnHover("Model Set ID"u8);

        Im.Table.NextColumn();
        ImEx.TextFramed(identifier.Attribute.ToNameU8(), default, FrameColor);
        Im.Tooltip.OnHover("Equip Slot"u8);
    }

    private static bool DrawEntry(RspEntry defaultEntry, ref RspEntry entry, bool disabled)
    {
        using var dis = Im.Disabled(disabled);
        Im.Table.NextColumn();
        var ret = DragInput("##rspValue"u8, [],                Im.Style.GlobalScale * 150, entry.Value, defaultEntry.Value, out var newValue,
            RspEntry.MinValue,              RspEntry.MaxValue, 0.001f,                     !disabled);
        if (ret)
            entry = new RspEntry(newValue);
        return ret;
    }

    public static bool DrawSubRace(ref RspIdentifier identifier, float unscaledWidth = 150)
    {
        var ret = Combos.Clan.Draw("##rspSubRace"u8, identifier.SubRace, "Racial Clan"u8, unscaledWidth * Im.Style.GlobalScale, out var subRace);
        if (ret)
            identifier = identifier with { SubRace = subRace };
        return ret;
    }

    public static bool DrawAttribute(ref RspIdentifier identifier, float unscaledWidth = 200)
    {
        var ret = Combos.RspType.Draw("##rspAttribute"u8, identifier.Attribute, "Scaling Attribute"u8, unscaledWidth, out var attribute);
        if (ret)
            identifier = identifier with { Attribute = attribute };
        return ret;
    }
}
