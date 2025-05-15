using Dalamud.Interface;
using ImGuiNET;
using Newtonsoft.Json.Linq;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.GameData.Enums;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow.Meta;

public sealed class ShpMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<ShpIdentifier, ShpEntry>(editor, metaFiles), IService
{
    public override ReadOnlySpan<byte> Label
        => "Shape Keys (SHP)###SHP"u8;

    private ShapeString _buffer          = ShapeString.TryRead("shpx_"u8, out var s) ? s : ShapeString.Empty;
    private ShapeString _conditionBuffer = ShapeString.Empty;
    private bool        _identifierValid;
    private bool        _conditionValid = true;

    public override int NumColumns
        => 7;

    public override float ColumnHeight
        => ImUtf8.FrameHeightSpacing;

    protected override void Initialize()
    {
        Identifier = new ShpIdentifier(HumanSlot.Unknown, null, ShapeString.Empty, ShapeString.Empty);
    }

    protected override void DrawNew()
    {
        ImGui.TableNextColumn();
        CopyToClipboardButton("Copy all current SHP manipulations to clipboard."u8,
            new Lazy<JToken?>(() => MetaDictionary.SerializeTo([], Editor.Shp)));

        ImGui.TableNextColumn();
        var canAdd = !Editor.Contains(Identifier) && _identifierValid && _conditionValid;
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
            .ThenBy(kvp => kvp.Key.ShapeCondition)
            .Select(kvp => (kvp.Key, kvp.Value));

    protected override int Count
        => Editor.Shp.Count;

    private bool DrawIdentifierInput(ref ShpIdentifier identifier)
    {
        ImGui.TableNextColumn();
        var changes = DrawHumanSlot(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawPrimaryId(ref identifier);

        ImGui.TableNextColumn();
        changes |= DrawShapeKeyInput(ref identifier, ref _buffer, ref _identifierValid);

        ImGui.TableNextColumn();
        changes |= DrawShapeConditionInput(ref identifier, ref _conditionBuffer, ref _conditionValid);
        return changes;
    }

    private static void DrawIdentifier(ShpIdentifier identifier)
    {
        ImGui.TableNextColumn();

        ImUtf8.TextFramed(SlotName(identifier.Slot), FrameColor);
        ImUtf8.HoverTooltip("Model Slot"u8);

        ImGui.TableNextColumn();
        if (identifier.Id.HasValue)
            ImUtf8.TextFramed($"{identifier.Id.Value.Id}", FrameColor);
        else
            ImUtf8.TextFramed("All IDs"u8, FrameColor);
        ImUtf8.HoverTooltip("Primary ID"u8);

        ImGui.TableNextColumn();
        ImUtf8.TextFramed(identifier.Shape.AsSpan, FrameColor);

        ImGui.TableNextColumn();
        if (identifier.ShapeCondition.Length > 0)
        {
            ImUtf8.TextFramed(identifier.ShapeCondition.AsSpan, FrameColor);
            ImUtf8.HoverTooltip("Connector condition for this shape to be activated.");
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
        ImUtf8.HoverTooltip("Whether to enable or disable this shape key for the selected items.");
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

        ImUtf8.HoverTooltip(allSlots ? "When using all slots, you also need to use all IDs."u8 : "Enable this shape key for all model IDs."u8);

        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        if (all)
        {
            using var style = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(0.05f, 0.5f));
            ImUtf8.TextFramed("All IDs"u8, ImGui.GetColorU32(ImGuiCol.FrameBg, all || allSlots ? ImGui.GetStyle().DisabledAlpha : 1f),
                new Vector2(unscaledWidth, 0), ImGui.GetColorU32(ImGuiCol.TextDisabled));
        }
        else
        {
            if (IdInput("##shpPrimaryId"u8, unscaledWidth, identifier.Id.GetValueOrDefault(0).Id, out var setId, 0,
                    ExpandedEqpGmpBase.Count - 1,
                    false))
            {
                identifier = identifier with { Id = setId };
                ret        = true;
            }
        }

        ImUtf8.HoverTooltip("Primary ID - You can usually find this as the 'e####' part of an item path or similar for customizations."u8);

        return ret;
    }

    public bool DrawHumanSlot(ref ShpIdentifier identifier, float unscaledWidth = 150)
    {
        var ret = false;
        ImGui.SetNextItemWidth(unscaledWidth * ImUtf8.GlobalScale);
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
                        if (_conditionBuffer.Length > 0
                         && (_conditionBuffer.IsAnkle() && slot is not HumanSlot.Feet and not HumanSlot.Legs
                             || _conditionBuffer.IsWrist() && slot is not HumanSlot.Hands and not HumanSlot.Body
                             || _conditionBuffer.IsWaist() && slot is not HumanSlot.Body and not HumanSlot.Legs))
                        {
                            identifier = identifier with
                            {
                                Slot = slot,
                                ShapeCondition = ShapeString.Empty,
                            };
                            _conditionValid = false;
                        }
                        else
                        {
                            identifier = identifier with
                            {
                                Slot = slot,
                                ShapeCondition = _conditionBuffer,
                            };
                            _conditionValid = true;
                        }
                    }
                }
        }

        ImUtf8.HoverTooltip("Model Slot"u8);
        return ret;
    }

    public static unsafe bool DrawShapeKeyInput(ref ShpIdentifier identifier, ref ShapeString buffer, ref bool valid, float unscaledWidth = 150)
    {
        var ret  = false;
        var ptr  = Unsafe.AsPointer(ref buffer);
        var span = new Span<byte>(ptr, ShapeString.MaxLength + 1);
        using (new ImRaii.ColorStyle().Push(ImGuiCol.Border, Colors.RegexWarningBorder, !valid).Push(ImGuiStyleVar.FrameBorderSize, 1f, !valid))
        {
            ImGui.SetNextItemWidth(unscaledWidth * ImUtf8.GlobalScale);
            if (ImUtf8.InputText("##shpShape"u8, span, out int newLength, "Shape Key..."u8))
            {
                buffer.ForceLength((byte)newLength);
                valid = ShpIdentifier.ValidateCustomShapeString(buffer);
                if (valid)
                    identifier = identifier with { Shape = buffer };
                ret = true;
            }
        }

        ImUtf8.HoverTooltip("Supported shape keys need to have the format `shpx_*` and a maximum length of 30 characters."u8);
        return ret;
    }

    public static unsafe bool DrawShapeConditionInput(ref ShpIdentifier identifier, ref ShapeString buffer, ref bool valid,
        float unscaledWidth = 150)
    {
        var ret  = false;
        var ptr  = Unsafe.AsPointer(ref buffer);
        var span = new Span<byte>(ptr, ShapeString.MaxLength + 1);
        using (new ImRaii.ColorStyle().Push(ImGuiCol.Border, Colors.RegexWarningBorder, !valid).Push(ImGuiStyleVar.FrameBorderSize, 1f, !valid))
        {
            ImGui.SetNextItemWidth(unscaledWidth * ImUtf8.GlobalScale);
            if (ImUtf8.InputText("##shpCondition"u8, span, out int newLength, "Shape Condition..."u8))
            {
                buffer.ForceLength((byte)newLength);
                valid = ShpIdentifier.ValidateCustomShapeString(buffer)
                 && (buffer.IsAnkle() && identifier.Slot is HumanSlot.Unknown or HumanSlot.Feet or HumanSlot.Legs
                     || buffer.IsWaist() && identifier.Slot is HumanSlot.Unknown or HumanSlot.Body or HumanSlot.Legs
                     || buffer.IsWrist() && identifier.Slot is HumanSlot.Unknown or HumanSlot.Body or HumanSlot.Hands);
                if (valid)
                    identifier = identifier with { ShapeCondition = buffer };
                ret = true;
            }
        }

        ImUtf8.HoverTooltip(
            "Supported conditional shape keys need to have the format `shpx_an_*` (Legs or Feet), `shpx_wr_*` (Body or Hands), or `shpx_wa_*` (Body or Legs) and a maximum length of 30 characters."u8);
        return ret;
    }

    private static ReadOnlySpan<HumanSlot> AvailableSlots
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

    private static ReadOnlySpan<byte> SlotName(HumanSlot slot)
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
