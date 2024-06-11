using Dalamud.Interface;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow.Meta;

public sealed class ImcMetaDrawer(ModEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<ImcIdentifier, ImcEntry>(editor, metaFiles), IService
{
    private bool _fileExists;

    private const string ModelSetIdTooltipShort = "Model Set ID";
    private const string EquipSlotTooltip       = "Equip Slot";
    private const string ModelRaceTooltip       = "Model Race";
    private const string GenderTooltip          = "Gender";
    private const string ObjectTypeTooltip      = "Object Type";
    private const string SecondaryIdTooltip     = "Secondary ID";
    private const string PrimaryIdTooltipShort  = "Primary ID";
    private const string VariantIdTooltip       = "Variant ID";
    private const string EstTypeTooltip         = "EST Type";
    private const string RacialTribeTooltip     = "Racial Tribe";
    private const string ScalingTypeTooltip     = "Scaling Type";

    protected override void Initialize()
    {
        Identifier = ImcIdentifier.Default;
        UpdateEntry();
    }

    private void UpdateEntry()
        => (Entry, _fileExists, _) = MetaFiles.ImcChecker.GetDefaultEntry(Identifier, true);

    protected override void DrawNew()
    {
        ImGui.TableNextColumn();
        // Copy To Clipboard
        ImGui.TableNextColumn();
        var canAdd = _fileExists && Editor.MetaEditor.CanAdd(Identifier);
        var tt     = canAdd ? "Stage this edit."u8 : !_fileExists ? "This IMC file does not exist."u8 : "This entry is already edited."u8;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, tt, disabled: !canAdd))
            Editor.MetaEditor.TryAdd(Identifier, Entry);

        if (DrawIdentifier(ref Identifier))
            UpdateEntry();

        using var disabled = ImRaii.Disabled();
        DrawEntry(Entry, ref Entry, false);
    }

    protected override void DrawEntry(ImcIdentifier identifier, ImcEntry entry)
    {
        const uint frameColor = 0;
        // Meta Buttons

        ImGui.TableNextColumn();
        ImUtf8.TextFramed(identifier.ObjectType.ToName(), frameColor);
        ImUtf8.HoverTooltip("Object Type"u8);

        ImGui.TableNextColumn();
        ImUtf8.TextFramed($"{identifier.PrimaryId.Id}", frameColor);
        ImUtf8.HoverTooltip("Primary ID");

        ImGui.TableNextColumn();
        if (identifier.ObjectType is ObjectType.Equipment or ObjectType.Accessory)
        {
            ImUtf8.TextFramed(identifier.EquipSlot.ToName(), frameColor);
            ImUtf8.HoverTooltip("Equip Slot"u8);
        }
        else
        {
            ImUtf8.TextFramed($"{identifier.SecondaryId.Id}", frameColor);
            ImUtf8.HoverTooltip("Secondary ID"u8);
        }

        ImGui.TableNextColumn();
        ImUtf8.TextFramed($"{identifier.Variant.Id}", frameColor);
        ImUtf8.HoverTooltip("Variant"u8);

        ImGui.TableNextColumn();
        if (identifier.ObjectType is ObjectType.DemiHuman)
        {
            ImUtf8.TextFramed(identifier.EquipSlot.ToName(), frameColor);
            ImUtf8.HoverTooltip("Equip Slot"u8);
        }

        var defaultEntry = MetaFiles.ImcChecker.GetDefaultEntry(identifier, true).Entry;
        if (DrawEntry(defaultEntry, ref entry, true))
            Editor.MetaEditor.Update(identifier, entry);
    }

    private static bool DrawIdentifier(ref ImcIdentifier identifier)
    {
        ImGui.TableNextColumn();
        var change = DrawObjectType(ref identifier);

        ImGui.TableNextColumn();
        change |= DrawPrimaryId(ref identifier);

        ImGui.TableNextColumn();
        if (identifier.ObjectType is ObjectType.Equipment or ObjectType.Accessory)
            change |= DrawSlot(ref identifier);
        else
            change |= DrawSecondaryId(ref identifier);

        ImGui.TableNextColumn();
        change |= DrawVariant(ref identifier);

        ImGui.TableNextColumn();
        if (identifier.ObjectType is ObjectType.DemiHuman)
            change |= DrawSlot(ref identifier, 70f);
        else
            ImUtf8.ScaledDummy(70f);
        return change;
    }

    private static bool DrawEntry(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault)
    {
        ImGui.TableNextColumn();
        var change = DrawMaterialId(defaultEntry, ref entry, addDefault);
        ImUtf8.SameLineInner();
        change |= DrawMaterialAnimationId(defaultEntry, ref entry, addDefault);

        ImGui.TableNextColumn();
        change |= DrawDecalId(defaultEntry, ref entry, addDefault);
        ImUtf8.SameLineInner();
        change |= DrawVfxId(defaultEntry, ref entry, addDefault);
        ImUtf8.SameLineInner();
        change |= DrawSoundId(defaultEntry, ref entry, addDefault);

        ImGui.TableNextColumn();
        change |= DrawAttributes(defaultEntry, ref entry);
        return change;
    }


    protected override IEnumerable<(ImcIdentifier, ImcEntry)> Enumerate()
        => Editor.MetaEditor.Imc.Select(kvp => (kvp.Key, kvp.Value));

    public static bool DrawObjectType(ref ImcIdentifier identifier, float width = 110)
    {
        var ret = Combos.ImcType("##imcType", identifier.ObjectType, out var type, width);
        ImUtf8.HoverTooltip("Object Type"u8);

        if (ret)
        {
            var equipSlot = type switch
            {
                ObjectType.Equipment => identifier.EquipSlot.IsEquipment() ? identifier.EquipSlot : EquipSlot.Head,
                ObjectType.DemiHuman => identifier.EquipSlot.IsEquipment() ? identifier.EquipSlot : EquipSlot.Head,
                ObjectType.Accessory => identifier.EquipSlot.IsAccessory() ? identifier.EquipSlot : EquipSlot.Ears,
                _                    => EquipSlot.Unknown,
            };
            identifier = identifier with
            {
                ObjectType = type,
                EquipSlot = equipSlot,
                SecondaryId = identifier.SecondaryId == 0 ? 1 : identifier.SecondaryId,
            };
        }

        return ret;
    }

    public static bool DrawPrimaryId(ref ImcIdentifier identifier, float unscaledWidth = 80)
    {
        var ret = IdInput("##imcPrimaryId"u8, unscaledWidth, identifier.PrimaryId.Id, out var newId, 0, ushort.MaxValue,
            identifier.PrimaryId.Id <= 1);
        ImUtf8.HoverTooltip("Primary ID - You can usually find this as the 'x####' part of an item path.\n"u8
          + "This should generally not be left <= 1 unless you explicitly want that."u8);
        if (ret)
            identifier = identifier with { PrimaryId = newId };
        return ret;
    }

    public static bool DrawSecondaryId(ref ImcIdentifier identifier, float unscaledWidth = 100)
    {
        var ret = IdInput("##imcSecondaryId"u8, unscaledWidth, identifier.SecondaryId.Id, out var newId, 0, ushort.MaxValue, false);
        ImUtf8.HoverTooltip("Secondary ID"u8);
        if (ret)
            identifier = identifier with { SecondaryId = newId };
        return ret;
    }

    public static bool DrawVariant(ref ImcIdentifier identifier, float unscaledWidth = 45)
    {
        var ret = IdInput("##imcVariant"u8, unscaledWidth, identifier.Variant.Id, out var newId, 0, byte.MaxValue, false);
        ImUtf8.HoverTooltip("Variant ID"u8);
        if (ret)
            identifier = identifier with { Variant = (byte)newId };
        return ret;
    }

    public static bool DrawSlot(ref ImcIdentifier identifier, float unscaledWidth = 100)
    {
        bool      ret;
        EquipSlot slot;
        switch (identifier.ObjectType)
        {
            case ObjectType.Equipment:
            case ObjectType.DemiHuman:
                ret = Combos.EqpEquipSlot("##slot", identifier.EquipSlot, out slot, unscaledWidth);
                break;
            case ObjectType.Accessory:
                ret = Combos.AccessorySlot("##slot", identifier.EquipSlot, out slot, unscaledWidth);
                break;
            default: return false;
        }

        ImUtf8.HoverTooltip("Equip Slot"u8);
        if (ret)
            identifier = identifier with { EquipSlot = slot };
        return ret;
    }

    public static bool DrawMaterialId(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault, float unscaledWidth = 45)
    {
        if (!DragInput("##materialId"u8, "Material ID"u8, unscaledWidth * ImUtf8.GlobalScale, entry.MaterialId, defaultEntry.MaterialId,
                out var newValue,        (byte)1,         byte.MaxValue,                      0.01f,            addDefault))
            return false;

        entry = entry with { MaterialId = newValue };
        return true;
    }

    public static bool DrawMaterialAnimationId(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault, float unscaledWidth = 45)
    {
        if (!DragInput("##mAnimId"u8,             "Material Animation ID"u8, unscaledWidth * ImUtf8.GlobalScale, entry.MaterialAnimationId,
                defaultEntry.MaterialAnimationId, out var newValue,          (byte)0, byte.MaxValue, 0.01f, addDefault))
            return false;

        entry = entry with { MaterialAnimationId = newValue };
        return true;
    }

    public static bool DrawDecalId(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault, float unscaledWidth = 45)
    {
        if (!DragInput("##decalId"u8, "Decal ID"u8,  unscaledWidth * ImUtf8.GlobalScale, entry.DecalId, defaultEntry.DecalId, out var newValue,
                (byte)0,              byte.MaxValue, 0.01f,                              addDefault))
            return false;

        entry = entry with { DecalId = newValue };
        return true;
    }

    public static bool DrawVfxId(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault, float unscaledWidth = 45)
    {
        if (!DragInput("##vfxId"u8, "VFX ID"u8, unscaledWidth * ImUtf8.GlobalScale, entry.VfxId, defaultEntry.VfxId, out var newValue, (byte)0,
                byte.MaxValue,      0.01f,      addDefault))
            return false;

        entry = entry with { VfxId = newValue };
        return true;
    }

    public static bool DrawSoundId(ImcEntry defaultEntry, ref ImcEntry entry, bool addDefault, float unscaledWidth = 45)
    {
        if (!DragInput("##soundId"u8, "Sound ID"u8,  unscaledWidth * ImUtf8.GlobalScale, entry.SoundId, defaultEntry.SoundId, out var newValue,
                (byte)0,              byte.MaxValue, 0.01f,                              addDefault))
            return false;

        entry = entry with { SoundId = newValue };
        return true;
    }

    public static bool DrawAttributes(ImcEntry defaultEntry, ref ImcEntry entry)
    {
        var changes = false;
        for (var i = 0; i < ImcEntry.NumAttributes; ++i)
        {
            using var id    = ImRaii.PushId(i);
            var       flag  = 1 << i;
            var       value = (entry.AttributeMask & flag) != 0;
            var       def   = (defaultEntry.AttributeMask & flag) != 0;
            if (Checkmark("##attribute"u8, "ABCDEFGHIJ"u8.Slice(i, 1), value, def, out var newValue))
            {
                var newMask = (ushort)(newValue ? entry.AttributeMask | flag : entry.AttributeMask & ~flag);
                entry   = entry with { AttributeMask = newMask };
                changes = true;
            }

            if (i < ImcEntry.NumAttributes - 1)
                ImUtf8.SameLineInner();
        }

        return changes;
    }


    /// <summary>
    /// A number input for ids with an optional max id of given width.
    /// Returns true if newId changed against currentId.
    /// </summary>
    private static bool IdInput(ReadOnlySpan<byte> label, float unscaledWidth, ushort currentId, out ushort newId, int minId, int maxId,
        bool border)
    {
        int tmp = currentId;
        ImGui.SetNextItemWidth(unscaledWidth * ImUtf8.GlobalScale);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, UiHelpers.Scale, border);
        using var color = ImRaii.PushColor(ImGuiCol.Border, Colors.RegexWarningBorder, border);
        if (ImUtf8.InputScalar(label, ref tmp))
            tmp = Math.Clamp(tmp, minId, maxId);

        newId = (ushort)tmp;
        return newId != currentId;
    }

    /// <summary>
    /// A dragging int input of given width that compares against a default value, shows a tooltip and clamps against min and max.
    /// Returns true if newValue changed against currentValue.
    /// </summary>
    private static bool DragInput<T>(ReadOnlySpan<byte> label, ReadOnlySpan<byte> tooltip, float width, T currentValue, T defaultValue,
        out T newValue, T minValue, T maxValue, float speed, bool addDefault) where T : unmanaged, INumber<T>
    {
        newValue = currentValue;
        using var color = ImRaii.PushColor(ImGuiCol.FrameBg,
            defaultValue > currentValue ? ColorId.DecreasedMetaValue.Value() : ColorId.IncreasedMetaValue.Value(),
            defaultValue != currentValue);
        ImGui.SetNextItemWidth(width);
        if (ImUtf8.DragScalar(label, ref newValue, minValue, maxValue, speed))
            newValue = newValue <= minValue ? minValue : newValue >= maxValue ? maxValue : newValue;

        if (addDefault)
            ImUtf8.HoverTooltip($"{tooltip}\nDefault Value: {defaultValue}");
        else
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, tooltip);

        return newValue != currentValue;
    }

    /// <summary>
    /// A checkmark that compares against a default value and shows a tooltip.
    /// Returns true if newValue is changed against currentValue.
    /// </summary>
    private static bool Checkmark(ReadOnlySpan<byte> label, ReadOnlySpan<byte> tooltip, bool currentValue, bool defaultValue,
        out bool newValue)
    {
        using var color = ImRaii.PushColor(ImGuiCol.FrameBg,
            defaultValue ? ColorId.DecreasedMetaValue.Value() : ColorId.IncreasedMetaValue.Value(),
            defaultValue != currentValue);
        newValue = currentValue;
        ImUtf8.Checkbox(label, ref newValue);
        ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, tooltip);
        return newValue != currentValue;
    }
}
