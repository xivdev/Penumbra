using Dalamud.Interface;
using OtterGui;
using Penumbra.GameData.Enums;
using Penumbra.Meta.Manipulations;

namespace Penumbra.UI.Classes;

public static class Combos
{
    // Different combos to use with enums.
    public static bool Race(string label, ModelRace current, out ModelRace race, float unscaledWidth = 100)
        => ImGuiUtil.GenericEnumCombo(label, unscaledWidth * UiHelpers.Scale, current, out race, RaceEnumExtensions.ToName, 1);

    public static bool Gender(string label, Gender current, out Gender gender, float unscaledWidth = 120)
        => ImGuiUtil.GenericEnumCombo(label, unscaledWidth, current, out gender, RaceEnumExtensions.ToName, 1);

    public static bool EqdpEquipSlot(string label, EquipSlot current, out EquipSlot slot, float unscaledWidth = 100)
        => ImGuiUtil.GenericEnumCombo(label, unscaledWidth * UiHelpers.Scale, current, out slot, EquipSlotExtensions.EqdpSlots,
            EquipSlotExtensions.ToName);

    public static bool EqpEquipSlot(string label, EquipSlot current, out EquipSlot slot, float unscaledWidth = 100)
        => ImGuiUtil.GenericEnumCombo(label, unscaledWidth * UiHelpers.Scale, current, out slot, EquipSlotExtensions.EquipmentSlots,
            EquipSlotExtensions.ToName);

    public static bool AccessorySlot(string label, EquipSlot current, out EquipSlot slot, float unscaledWidth = 100)
        => ImGuiUtil.GenericEnumCombo(label, unscaledWidth * UiHelpers.Scale, current, out slot, EquipSlotExtensions.AccessorySlots,
            EquipSlotExtensions.ToName);

    public static bool SubRace(string label, SubRace current, out SubRace subRace, float unscaledWidth = 150)
        => ImGuiUtil.GenericEnumCombo(label, unscaledWidth * UiHelpers.Scale, current, out subRace, RaceEnumExtensions.ToName, 1);

    public static bool RspAttribute(string label, RspAttribute current, out RspAttribute attribute, float unscaledWidth = 200)
        => ImGuiUtil.GenericEnumCombo(label, unscaledWidth * UiHelpers.Scale, current, out attribute,
            RspAttributeExtensions.ToFullString, 0, 1);

    public static bool EstSlot(string label, EstType current, out EstType attribute, float unscaledWidth = 200)
        => ImGuiUtil.GenericEnumCombo(label, unscaledWidth * UiHelpers.Scale, current, out attribute);

    public static bool ImcType(string label, ObjectType current, out ObjectType type, float unscaledWidth = 110)
        => ImGuiUtil.GenericEnumCombo(label, unscaledWidth * UiHelpers.Scale, current, out type, ObjectTypeExtensions.ValidImcTypes,
            ObjectTypeExtensions.ToName);
}
