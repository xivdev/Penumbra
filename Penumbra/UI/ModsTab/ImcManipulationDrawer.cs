using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using OtterGui.Text.HelperObjects;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta.Manipulations;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public static class ImcManipulationDrawer
{
    public static bool DrawObjectType(ref ImcManipulation manip, float width = 110)
    {
        var ret = Combos.ImcType("##imcType", manip.ObjectType, out var type, width);
        ImUtf8.HoverTooltip("Object Type"u8);

        if (ret)
        {
            var equipSlot = type switch
            {
                ObjectType.Equipment => manip.EquipSlot.IsEquipment() ? manip.EquipSlot : EquipSlot.Head,
                ObjectType.DemiHuman => manip.EquipSlot.IsEquipment() ? manip.EquipSlot : EquipSlot.Head,
                ObjectType.Accessory => manip.EquipSlot.IsAccessory() ? manip.EquipSlot : EquipSlot.Ears,
                _                    => EquipSlot.Unknown,
            };
            manip = new ImcManipulation(type, manip.BodySlot, manip.PrimaryId, manip.SecondaryId == 0 ? 1 : manip.SecondaryId,
                manip.Variant.Id, equipSlot, manip.Entry);
        }

        return ret;
    }

    public static bool DrawPrimaryId(ref ImcManipulation manip, float unscaledWidth = 80)
    {
        var ret = IdInput("##imcPrimaryId"u8, unscaledWidth, manip.PrimaryId.Id, out var newId, 0, ushort.MaxValue,
            manip.PrimaryId.Id <= 1);
        ImUtf8.HoverTooltip("Primary ID - You can usually find this as the 'x####' part of an item path.\n"u8
          + "This should generally not be left <= 1 unless you explicitly want that."u8);
        if (ret)
            manip = new ImcManipulation(manip.ObjectType, manip.BodySlot, newId, manip.SecondaryId, manip.Variant.Id, manip.EquipSlot,
                manip.Entry);
        return ret;
    }

    public static bool DrawSecondaryId(ref ImcManipulation manip, float unscaledWidth = 100)
    {
        var ret = IdInput("##imcSecondaryId"u8, unscaledWidth, manip.SecondaryId.Id, out var newId, 0, ushort.MaxValue, false);
        ImUtf8.HoverTooltip("Secondary ID"u8);
        if (ret)
            manip = new ImcManipulation(manip.ObjectType, manip.BodySlot, manip.PrimaryId, newId, manip.Variant.Id, manip.EquipSlot,
                manip.Entry);
        return ret;
    }

    public static bool DrawVariant(ref ImcManipulation manip, float unscaledWidth = 45)
    {
        var ret = IdInput("##imcVariant"u8, unscaledWidth, manip.Variant.Id, out var newId, 0, byte.MaxValue, false);
        ImUtf8.HoverTooltip("Variant ID"u8);
        if (ret)
            manip = new ImcManipulation(manip.ObjectType, manip.BodySlot, manip.PrimaryId, manip.SecondaryId, (byte)newId, manip.EquipSlot,
                manip.Entry);
        return ret;
    }

    public static bool DrawSlot(ref ImcManipulation manip, float unscaledWidth = 100)
    {
        bool      ret;
        EquipSlot slot;
        switch (manip.ObjectType)
        {
            case ObjectType.Equipment:
            case ObjectType.DemiHuman:
                ret = Combos.EqpEquipSlot("##slot", manip.EquipSlot, out slot, unscaledWidth);
                break;
            case ObjectType.Accessory:
                ret = Combos.AccessorySlot("##slot", manip.EquipSlot, out slot, unscaledWidth);
                break;
            default: return false;
        }

        ImUtf8.HoverTooltip("Equip Slot"u8);
        if (ret)
            manip = new ImcManipulation(manip.ObjectType, manip.BodySlot, manip.PrimaryId, manip.SecondaryId, manip.Variant.Id, slot,
                manip.Entry);
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
                ImGui.SameLine();
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
