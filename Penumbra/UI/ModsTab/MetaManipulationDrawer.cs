using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.GameData.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public static class MetaManipulationDrawer
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
}
