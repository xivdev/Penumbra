using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.GameData.Enums;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow.Meta;

public sealed class RspMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<RspIdentifier, RspEntry>(editor, metaFiles), IService
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
        ImGui.TableNextColumn();
        CopyToClipboardButton("Copy all current RSP manipulations to clipboard."u8, MetaDictionary.SerializeTo([], Editor.Rsp));

        ImGui.TableNextColumn();
        var canAdd = !Editor.Contains(Identifier);
        var tt     = canAdd ? "Stage this edit."u8 : "This entry is already edited."u8;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, tt, disabled: !canAdd))
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
        ImGui.TableNextColumn();
        var changes = DrawSubRace(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawAttribute(ref identifier);
        return changes;
    }

    private static void DrawIdentifier(RspIdentifier identifier)
    {
        ImGui.TableNextColumn();
        ImUtf8.TextFramed(identifier.SubRace.ToName(), FrameColor);
        ImUtf8.HoverTooltip("Model Set ID"u8);

        ImGui.TableNextColumn();
        ImUtf8.TextFramed(identifier.Attribute.ToFullString(), FrameColor);
        ImUtf8.HoverTooltip("Equip Slot"u8);
    }

    private static bool DrawEntry(RspEntry defaultEntry, ref RspEntry entry, bool disabled)
    {
        using var dis = ImRaii.Disabled(disabled);
        ImGui.TableNextColumn();
        var ret = DragInput("##rspValue"u8, [],                ImUtf8.GlobalScale * 150, entry.Value, defaultEntry.Value, out var newValue,
            RspEntry.MinValue,              RspEntry.MaxValue, 0.001f,                   !disabled);
        if (ret)
            entry = new RspEntry(newValue);
        return ret;
    }

    public static bool DrawSubRace(ref RspIdentifier identifier, float unscaledWidth = 150)
    {
        var ret = Combos.SubRace("##rspSubRace", identifier.SubRace, out var subRace, unscaledWidth);
        ImUtf8.HoverTooltip("Racial Clan"u8);
        if (ret)
            identifier = identifier with { SubRace = subRace };
        return ret;
    }

    public static bool DrawAttribute(ref RspIdentifier identifier, float unscaledWidth = 200)
    {
        var ret = Combos.RspAttribute("##rspAttribute", identifier.Attribute, out var attribute, unscaledWidth);
        ImUtf8.HoverTooltip("Scaling Attribute"u8);
        if (ret)
            identifier = identifier with { Attribute = attribute };
        return ret;
    }
}
