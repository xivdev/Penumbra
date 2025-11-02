using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using ImSharp;
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

public sealed class ShpMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<ShpIdentifier, ShpEntry>(editor, metaFiles)
{
    public override ReadOnlySpan<byte> Label
        => "Shape Keys (SHP)###SHP"u8;

    private ShapeAttributeString _buffer = ShapeAttributeString.TryRead("shpx_"u8, out var s) ? s : ShapeAttributeString.Empty;
    private bool                 _identifierValid;

    public override int NumColumns
        => 8;

    public override float ColumnHeight
        => ImUtf8.FrameHeightSpacing;

    protected override void Initialize()
    {
        Identifier = new ShpIdentifier(HumanSlot.Unknown, null, ShapeAttributeString.Empty, ShapeConnectorCondition.None, GenderRace.Unknown);
    }

    protected override void DrawNew()
    {
        ImGui.TableNextColumn();
        CopyToClipboardButton("Copy all current SHP manipulations to clipboard."u8,
            new Lazy<JToken?>(() => MetaDictionary.SerializeTo([], Editor.Shp)));

        ImGui.TableNextColumn();
        var canAdd = !Editor.Contains(Identifier) && _identifierValid;
        var tt = canAdd
            ? "Stage this edit."u8
            : _identifierValid
                ? "This entry does not contain a valid shape key."u8
                : "This entry is already edited."u8;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, tt, disabled: !canAdd))
            Editor.Changes |= Editor.TryAdd(Identifier, ShpEntry.True);

        DrawIdentifierInput(ref Identifier);
        DrawEntry(ref Entry, true);
    }

    protected override void DrawEntry(ShpIdentifier identifier, ShpEntry entry)
    {
        DrawMetaButtons(identifier, entry);
        DrawIdentifier(identifier);

        if (DrawEntry(ref entry, false))
            Editor.Changes |= Editor.Update(identifier, entry);
    }

    protected override IEnumerable<(ShpIdentifier, ShpEntry)> Enumerate()
        => Editor.Shp
            .OrderBy(kvp => kvp.Key.Shape)
            .ThenBy(kvp => kvp.Key.Slot)
            .ThenBy(kvp => kvp.Key.Id)
            .ThenBy(kvp => kvp.Key.ConnectorCondition)
            .Select(kvp => (kvp.Key, kvp.Value));

    protected override int Count
        => Editor.Shp.Count;

    private bool DrawIdentifierInput(ref ShpIdentifier identifier)
    {
        ImGui.TableNextColumn();
        var changes = DrawHumanSlot(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawGenderRaceConditionInput(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawPrimaryId(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawShapeKeyInput(ref identifier, ref _buffer, ref _identifierValid);

        ImGui.TableNextColumn();
        changes |= DrawConnectorConditionInput(ref identifier);
        return changes;
    }

    private static void DrawIdentifier(ShpIdentifier identifier)
    {
        ImGui.TableNextColumn();

        ImUtf8.TextFramed(SlotName(identifier.Slot), FrameColor);
        Im.Tooltip.OnHover("Model Slot"u8);

        ImGui.TableNextColumn();
        if (identifier.GenderRaceCondition is not GenderRace.Unknown)
        {
            ImUtf8.TextFramed($"{identifier.GenderRaceCondition.ToName()} ({identifier.GenderRaceCondition.ToRaceCode()})", FrameColor);
            Im.Tooltip.OnHover("Gender & Race Code for this shape key to be set.");
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
        Im.Tooltip.OnHover("Primary ID"u8);

        ImGui.TableNextColumn();
        ImUtf8.TextFramed(identifier.Shape.AsSpan, FrameColor);

        ImGui.TableNextColumn();
        if (identifier.ConnectorCondition is not ShapeConnectorCondition.None)
        {
            ImUtf8.TextFramed($"{identifier.ConnectorCondition}", FrameColor);
            Im.Tooltip.OnHover("Connector condition for this shape to be activated.");
        }
    }

    private static bool DrawEntry(ref ShpEntry entry, bool disabled)
    {
        using var dis = ImRaii.Disabled(disabled);
        ImGui.TableNextColumn();
        var value   = entry.Value;
        var changes = ImUtf8.Checkbox("##shpEntry"u8, ref value);
        if (changes)
            entry = new ShpEntry(value);
        Im.Tooltip.OnHover("Whether to enable or disable this shape key for the selected items.");
        return changes;
    }

    public static bool DrawPrimaryId(ref ShpIdentifier identifier, float unscaledWidth = 100)
    {
        var allSlots = identifier.Slot is HumanSlot.Unknown;
        var all      = !identifier.Id.HasValue;
        var ret      = false;
        using (ImRaii.Disabled(allSlots))
        {
            if (ImUtf8.Checkbox("##shpAll"u8, ref all))
            {
                identifier = identifier with { Id = all ? null : 0 };
                ret        = true;
            }
        }

        Im.Tooltip.OnHover(allSlots ? "When using all slots, you also need to use all IDs."u8 : "Enable this shape key for all model IDs."u8);

        Im.Line.Same(0, Im.Style.ItemInnerSpacing.X);
        if (all)
        {
            using var style = ImStyleDouble.ButtonTextAlign.Push(new Vector2(0.05f, 0.5f));
            ImUtf8.TextFramed("All IDs"u8, ImGuiColor.FrameBackground.Get(all || allSlots ? Im.Style.DisabledAlpha : 1f).Color,
                new Vector2(unscaledWidth, 0), ImGuiColor.TextDisabled.Get().Color);
        }
        else
        {
            var max = identifier.Slot.ToSpecificEnum() is BodySlot ? byte.MaxValue : ExpandedEqpGmpBase.Count - 1;
            if (IdInput("##shpPrimaryId"u8, unscaledWidth, identifier.Id.GetValueOrDefault(0).Id, out var setId, 0, max, false))
            {
                identifier = identifier with { Id = setId };
                ret        = true;
            }
        }

        Im.Tooltip.OnHover("Primary ID - You can usually find this as the 'e####' part of an item path or similar for customizations."u8);

        return ret;
    }

    public bool DrawHumanSlot(ref ShpIdentifier identifier, float unscaledWidth = 170)
    {
        var ret = false;
        Im.Item.SetNextWidth(unscaledWidth * Im.Style.GlobalScale);
        using (var combo = ImUtf8.Combo("##shpSlot"u8, SlotName(identifier.Slot)))
        {
            if (combo)
                foreach (var slot in AvailableSlots)
                {
                    if (!ImUtf8.Selectable(SlotName(slot), slot == identifier.Slot) || slot == identifier.Slot)
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
                            ConnectorCondition = Identifier.ConnectorCondition switch
                            {
                                ShapeConnectorCondition.Wrists when slot is HumanSlot.Body or HumanSlot.Hands => ShapeConnectorCondition.Wrists,
                                ShapeConnectorCondition.Waist when slot is HumanSlot.Body or HumanSlot.Legs   => ShapeConnectorCondition.Waist,
                                ShapeConnectorCondition.Ankles when slot is HumanSlot.Legs or HumanSlot.Feet  => ShapeConnectorCondition.Ankles,
                                _                                                                             => ShapeConnectorCondition.None,
                            },
                        };
                        ret = true;
                    }
                }
        }

        Im.Tooltip.OnHover("Model Slot"u8);
        return ret;
    }

    public static unsafe bool DrawShapeKeyInput(ref ShpIdentifier identifier, ref ShapeAttributeString buffer, ref bool valid,
        float unscaledWidth = 200)
    {
        var ret  = false;
        var ptr  = Unsafe.AsPointer(ref buffer);
        var span = new Span<byte>(ptr, ShapeAttributeString.MaxLength + 1);
        using (ImStyleBorder.Frame.Push(Colors.RegexWarningBorder, Im.Style.GlobalScale, !valid))
        {
            Im.Item.SetNextWidth(unscaledWidth * Im.Style.GlobalScale);
            if (ImUtf8.InputText("##shpShape"u8, span, out int newLength, "Shape Key..."u8))
            {
                buffer.ForceLength((byte)newLength);
                valid = buffer.ValidateCustomShapeString();
                if (valid)
                    identifier = identifier with { Shape = buffer };
                ret = true;
            }
        }

        Im.Tooltip.OnHover("Supported shape keys need to have the format `shpx_*` and a maximum length of 30 characters."u8);
        return ret;
    }

    private static bool DrawConnectorConditionInput(ref ShpIdentifier identifier, float unscaledWidth = 80)
    {
        var ret = false;
        Im.Item.SetNextWidth(unscaledWidth * Im.Style.GlobalScale);
        var (showWrists, showWaist, showAnkles, disable) = identifier.Slot switch
        {
            HumanSlot.Unknown => (true, true, true, false),
            HumanSlot.Body    => (true, true, false, false),
            HumanSlot.Legs    => (false, true, true, false),
            HumanSlot.Hands   => (true, false, false, false),
            HumanSlot.Feet    => (false, false, true, false),
            _                 => (false, false, false, true),
        };
        using var disabled = ImRaii.Disabled(disable);
        using (var combo = ImUtf8.Combo("##shpCondition"u8, $"{identifier.ConnectorCondition}"))
        {
            if (combo)
            {
                if (ImUtf8.Selectable("None"u8, identifier.ConnectorCondition is ShapeConnectorCondition.None))
                    identifier = identifier with { ConnectorCondition = ShapeConnectorCondition.None };

                if (showWrists && ImUtf8.Selectable("Wrists"u8, identifier.ConnectorCondition is ShapeConnectorCondition.Wrists))
                    identifier = identifier with { ConnectorCondition = ShapeConnectorCondition.Wrists };

                if (showWaist && ImUtf8.Selectable("Waist"u8, identifier.ConnectorCondition is ShapeConnectorCondition.Waist))
                    identifier = identifier with { ConnectorCondition = ShapeConnectorCondition.Waist };

                if (showAnkles && ImUtf8.Selectable("Ankles"u8, identifier.ConnectorCondition is ShapeConnectorCondition.Ankles))
                    identifier = identifier with { ConnectorCondition = ShapeConnectorCondition.Ankles };
            }
        }

        Im.Tooltip.OnHover(
            "Only activate this shape key if any custom connector shape keys (shpx_[wr|wa|an]_*) are also enabled through matching attributes."u8);
        return ret;
    }

    private static bool DrawGenderRaceConditionInput(ref ShpIdentifier identifier, float unscaledWidth = 250)
    {
        var ret = false;
        Im.Item.SetNextWidth(unscaledWidth * Im.Style.GlobalScale);

        using (var combo = ImUtf8.Combo("##shpGenderRace"u8, identifier.GenderRaceCondition is GenderRace.Unknown
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

        Im.Tooltip.OnHover(
            "Only activate this shape key for this gender & race code."u8);

        return ret;
    }

    public static ReadOnlySpan<HumanSlot> AvailableSlots
        =>
        [
            HumanSlot.Unknown,
            HumanSlot.Head,
            HumanSlot.Body,
            HumanSlot.Hands,
            HumanSlot.Legs,
            HumanSlot.Feet,
            HumanSlot.Ears,
            HumanSlot.Neck,
            HumanSlot.Wrists,
            HumanSlot.RFinger,
            HumanSlot.LFinger,
            HumanSlot.Glasses,
            HumanSlot.Hair,
            HumanSlot.Face,
            HumanSlot.Ear,
        ];

    public static ReadOnlySpan<byte> SlotName(HumanSlot slot)
        => slot switch
        {
            HumanSlot.Unknown => "All Slots"u8,
            HumanSlot.Head    => "Equipment: Head"u8,
            HumanSlot.Body    => "Equipment: Body"u8,
            HumanSlot.Hands   => "Equipment: Hands"u8,
            HumanSlot.Legs    => "Equipment: Legs"u8,
            HumanSlot.Feet    => "Equipment: Feet"u8,
            HumanSlot.Ears    => "Equipment: Ears"u8,
            HumanSlot.Neck    => "Equipment: Neck"u8,
            HumanSlot.Wrists  => "Equipment: Wrists"u8,
            HumanSlot.RFinger => "Equipment: Right Finger"u8,
            HumanSlot.LFinger => "Equipment: Left Finger"u8,
            HumanSlot.Glasses => "Equipment: Glasses"u8,
            HumanSlot.Hair    => "Customization: Hair"u8,
            HumanSlot.Face    => "Customization: Face"u8,
            HumanSlot.Ear     => "Customization: Ears"u8,
            _                 => "Unknown"u8,
        };
}
