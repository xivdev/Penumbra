using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json.Linq;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.Collections.Cache;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow.Meta;

public sealed class AtrMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<AtrIdentifier, AtrEntry>(editor, metaFiles), Luna.IService
{
    public override ReadOnlySpan<byte> Label
        => "Attributes(ATR)###ATR"u8;

    private ShapeAttributeString _buffer = ShapeAttributeString.TryRead("atrx_"u8, out var s) ? s : ShapeAttributeString.Empty;
    private bool                 _identifierValid;

    public override int NumColumns
        => 7;

    public override float ColumnHeight
        => ImUtf8.FrameHeightSpacing;

    protected override void Initialize()
    {
        Identifier = new AtrIdentifier(HumanSlot.Unknown, null, ShapeAttributeString.Empty, GenderRace.Unknown);
        Entry      = AtrEntry.True;
    }

    protected override void DrawNew()
    {
        ImGui.TableNextColumn();
        CopyToClipboardButton("Copy all current ATR manipulations to clipboard."u8,
            new Lazy<JToken?>(() => MetaDictionary.SerializeTo([], Editor.Atr)));

        ImGui.TableNextColumn();
        var canAdd = !Editor.Contains(Identifier) && _identifierValid;
        var tt = canAdd
            ? "Stage this edit."u8
            : _identifierValid
                ? "This entry does not contain a valid attribute."u8
                : "This entry is already edited."u8;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, tt, disabled: !canAdd))
            Editor.Changes |= Editor.TryAdd(Identifier, AtrEntry.False);

        DrawIdentifierInput(ref Identifier);
        DrawEntry(ref Entry, true);
    }

    protected override void DrawEntry(AtrIdentifier identifier, AtrEntry entry)
    {
        DrawMetaButtons(identifier, entry);
        DrawIdentifier(identifier);

        if (DrawEntry(ref entry, false))
            Editor.Changes |= Editor.Update(identifier, entry);
    }

    protected override IEnumerable<(AtrIdentifier, AtrEntry)> Enumerate()
        => Editor.Atr
            .OrderBy(kvp => kvp.Key.Attribute)
            .ThenBy(kvp => kvp.Key.Slot)
            .ThenBy(kvp => kvp.Key.Id)
            .Select(kvp => (kvp.Key, kvp.Value));

    protected override int Count
        => Editor.Atr.Count;

    private bool DrawIdentifierInput(ref AtrIdentifier identifier)
    {
        ImGui.TableNextColumn();
        var changes = DrawHumanSlot(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawGenderRaceConditionInput(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawPrimaryId(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawAttributeKeyInput(ref identifier, ref _buffer, ref _identifierValid);
        return changes;
    }

    private static void DrawIdentifier(AtrIdentifier identifier)
    {
        ImGui.TableNextColumn();

        ImUtf8.TextFramed(ShpMetaDrawer.SlotName(identifier.Slot), FrameColor);
        ImUtf8.HoverTooltip("Model Slot"u8);

        ImGui.TableNextColumn();
        if (identifier.GenderRaceCondition is not GenderRace.Unknown)
        {
            ImUtf8.TextFramed($"{identifier.GenderRaceCondition.ToName()} ({identifier.GenderRaceCondition.ToRaceCode()})", FrameColor);
            ImUtf8.HoverTooltip("Gender & Race Code for this attribute to be set.");
        }
        else
        {
            ImUtf8.TextFramed("Any Gender & Race"u8, FrameColor);
        }

        ImGui.TableNextColumn();
        if (identifier.Id.HasValue)
            ImUtf8.TextFramed($"{identifier.Id.Value.Id}", FrameColor);
        else
            ImUtf8.TextFramed("All IDs"u8, FrameColor);
        ImUtf8.HoverTooltip("Primary ID"u8);

        ImGui.TableNextColumn();
        ImUtf8.TextFramed(identifier.Attribute.AsSpan, FrameColor);
    }

    private static bool DrawEntry(ref AtrEntry entry, bool disabled)
    {
        using var dis = ImRaii.Disabled(disabled);
        ImGui.TableNextColumn();
        var value   = entry.Value;
        var changes = ImUtf8.Checkbox("##atrEntry"u8, ref value);
        if (changes)
            entry = new AtrEntry(value);
        ImUtf8.HoverTooltip("Whether to enable or disable this attribute for the selected items.");
        return changes;
    }

    public static bool DrawPrimaryId(ref AtrIdentifier identifier, float unscaledWidth = 100)
    {
        var allSlots = identifier.Slot is HumanSlot.Unknown;
        var all      = !identifier.Id.HasValue;
        var ret      = false;
        using (ImRaii.Disabled(allSlots))
        {
            if (ImUtf8.Checkbox("##atrAll"u8, ref all))
            {
                identifier = identifier with { Id = all ? null : 0 };
                ret        = true;
            }
        }

        ImUtf8.HoverTooltip(allSlots
            ? "When using all slots, you also need to use all IDs."u8
            : "Enable this attribute for all model IDs."u8);

        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        if (all)
        {
            using var style = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(0.05f, 0.5f));
            ImUtf8.TextFramed("All IDs"u8, ImGui.GetColorU32(ImGuiCol.FrameBg, all || allSlots ? ImGui.GetStyle().DisabledAlpha : 1f),
                new Vector2(unscaledWidth, 0), ImGui.GetColorU32(ImGuiCol.TextDisabled));
        }
        else
        {
            var max = identifier.Slot.ToSpecificEnum() is BodySlot ? byte.MaxValue : ExpandedEqpGmpBase.Count - 1;
            if (IdInput("##atrPrimaryId"u8, unscaledWidth, identifier.Id.GetValueOrDefault(0).Id, out var setId, 0, max, false))
            {
                identifier = identifier with { Id = setId };
                ret        = true;
            }
        }

        ImUtf8.HoverTooltip("Primary ID - You can usually find this as the 'e####' part of an item path or similar for customizations."u8);

        return ret;
    }

    public bool DrawHumanSlot(ref AtrIdentifier identifier, float unscaledWidth = 150)
    {
        var ret = false;
        ImGui.SetNextItemWidth(unscaledWidth * ImUtf8.GlobalScale);
        using (var combo = ImUtf8.Combo("##atrSlot"u8, ShpMetaDrawer.SlotName(identifier.Slot)))
        {
            if (combo)
                foreach (var slot in ShpMetaDrawer.AvailableSlots)
                {
                    if (!ImUtf8.Selectable(ShpMetaDrawer.SlotName(slot), slot == identifier.Slot) || slot == identifier.Slot)
                        continue;

                    ret = true;
                    if (slot is HumanSlot.Unknown)
                    {
                        identifier = identifier with
                        {
                            Id = null,
                            Slot = slot,
                        };
                    }
                    else
                    {
                        identifier = identifier with
                        {
                            Id = identifier.Id.HasValue
                                ? (PrimaryId)Math.Clamp(identifier.Id.Value.Id, 0,
                                    slot.ToSpecificEnum() is BodySlot ? byte.MaxValue : ExpandedEqpGmpBase.Count - 1)
                                : null,
                            Slot = slot,
                        };
                        ret = true;
                    }
                }
        }

        ImUtf8.HoverTooltip("Model Slot"u8);
        return ret;
    }
     
    private static bool DrawGenderRaceConditionInput(ref AtrIdentifier identifier, float unscaledWidth = 250)
    {
        var ret = false;
        ImGui.SetNextItemWidth(unscaledWidth * ImUtf8.GlobalScale);

        using (var combo = ImUtf8.Combo("##shpGenderRace"u8,
                   identifier.GenderRaceCondition is GenderRace.Unknown
                       ? "Any Gender & Race"
                       : $"{identifier.GenderRaceCondition.ToName()} ({identifier.GenderRaceCondition.ToRaceCode()})"))
        {
            if (combo)
            {
                if (ImUtf8.Selectable("Any Gender & Race"u8, identifier.GenderRaceCondition is GenderRace.Unknown)
                 && identifier.GenderRaceCondition is not GenderRace.Unknown)
                {
                    identifier = identifier with { GenderRaceCondition = GenderRace.Unknown };
                    ret        = true;
                }

                foreach (var gr in ShapeAttributeHashSet.GenderRaceValues.Skip(1))
                {
                    if (ImUtf8.Selectable($"{gr.ToName()} ({gr.ToRaceCode()})", identifier.GenderRaceCondition == gr)
                     && identifier.GenderRaceCondition != gr)
                    {
                        identifier = identifier with { GenderRaceCondition = gr };
                        ret        = true;
                    }
                }
            }
        }

        ImUtf8.HoverTooltip(
            "Only activate this attribute for this gender & race code."u8);

        return ret;
    }

    public static unsafe bool DrawAttributeKeyInput(ref AtrIdentifier identifier, ref ShapeAttributeString buffer, ref bool valid,
        float unscaledWidth = 150)
    {
        var ret  = false;
        var ptr  = Unsafe.AsPointer(ref buffer);
        var span = new Span<byte>(ptr, ShapeAttributeString.MaxLength + 1);
        using (new ImRaii.ColorStyle().Push(ImGuiCol.Border, Colors.RegexWarningBorder, !valid).Push(ImGuiStyleVar.FrameBorderSize, 1f, !valid))
        {
            ImGui.SetNextItemWidth(unscaledWidth * ImUtf8.GlobalScale);
            if (ImUtf8.InputText("##atrAttribute"u8, span, out int newLength, "Attribute..."u8))
            {
                buffer.ForceLength((byte)newLength);
                valid = buffer.ValidateCustomAttributeString();
                if (valid)
                    identifier = identifier with { Attribute = buffer };
                ret = true;
            }
        }

        ImUtf8.HoverTooltip("Supported attribute need to have the format `atrx_*` and a maximum length of 30 characters."u8);
        return ret;
    }
}
