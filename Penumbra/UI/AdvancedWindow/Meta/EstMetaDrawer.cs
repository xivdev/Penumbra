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

public sealed class EstMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<EstIdentifier, EstEntry>(editor, metaFiles), IService
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
        ImGui.TableNextColumn();
        CopyToClipboardButton("Copy all current EST manipulations to clipboard."u8, MetaDictionary.SerializeTo([], Editor.Est));

        ImGui.TableNextColumn();
        var canAdd = !Editor.Contains(Identifier);
        var tt     = canAdd ? "Stage this edit."u8 : "This entry is already edited."u8;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, tt, disabled: !canAdd))
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
        ImGui.TableNextColumn();
        var changes = DrawPrimaryId(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawRace(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawGender(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawSlot(ref identifier);

        return changes;
    }

    private static void DrawIdentifier(EstIdentifier identifier)
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
        ImUtf8.TextFramed(identifier.Slot.ToString(), FrameColor);
        ImUtf8.HoverTooltip("Extra Skeleton Type"u8);
    }

    private static bool DrawEntry(EstEntry defaultEntry, ref EstEntry entry, bool disabled)
    {
        using var dis = ImRaii.Disabled(disabled);
        ImGui.TableNextColumn();
        var ret = DragInput("##estValue"u8, [],    100f * ImUtf8.GlobalScale, entry.Value, defaultEntry.Value, out var newValue, (ushort)0,
            ushort.MaxValue,                0.05f, !disabled);
        if (ret)
            entry = new EstEntry(newValue);
        return ret;
    }

    public static bool DrawPrimaryId(ref EstIdentifier identifier, float unscaledWidth = 100)
    {
        var ret = IdInput("##estPrimaryId"u8, unscaledWidth, identifier.SetId.Id, out var setId, 0, ExpandedEqpGmpBase.Count - 1,
            identifier.SetId.Id <= 1);
        ImUtf8.HoverTooltip(
            "Model Set ID - You can usually find this as the 'e####' part of an item path.\nThis should generally not be left <= 1 unless you explicitly want that."u8);
        if (ret)
            identifier = identifier with { SetId = setId };
        return ret;
    }

    public static bool DrawRace(ref EstIdentifier identifier, float unscaledWidth = 100)
    {
        var ret = Combos.Race("##estRace", identifier.Race, out var race, unscaledWidth);
        ImUtf8.HoverTooltip("Model Race"u8);
        if (ret)
            identifier = identifier with { GenderRace = Names.CombinedRace(identifier.Gender, race) };
        return ret;
    }

    public static bool DrawGender(ref EstIdentifier identifier, float unscaledWidth = 120)
    {
        var ret = Combos.Gender("##estGender", identifier.Gender, out var gender, unscaledWidth);
        ImUtf8.HoverTooltip("Gender"u8);
        if (ret)
            identifier = identifier with { GenderRace = Names.CombinedRace(gender, identifier.Race) };
        return ret;
    }

    public static bool DrawSlot(ref EstIdentifier identifier, float unscaledWidth = 200)
    {
        var ret = Combos.EstSlot("##estSlot", identifier.Slot, out var slot, unscaledWidth);
        ImUtf8.HoverTooltip("Extra Skeleton Type"u8);
        if (ret)
            identifier = identifier with { Slot = slot };
        return ret;
    }
}
