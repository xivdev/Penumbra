using Dalamud.Interface;
using OtterGui;
using Penumbra.GameData.Enums;
using Penumbra.Meta.Manipulations;

namespace Penumbra.UI.Classes;

public static class Combos
{
    // Different combos to use with enums.
    public static bool Race( string label, ModelRace current, out ModelRace race )
        => Race( label, 100, current, out race );

    public static bool Race( string label, float unscaledWidth, ModelRace current, out ModelRace race )
        => ImGuiUtil.GenericEnumCombo( label, unscaledWidth * UiHelpers.Scale, current, out race, RaceEnumExtensions.ToName, 1 );

    public static bool Gender( string label, Gender current, out Gender gender )
        => Gender( label, 120, current, out gender );

    public static bool Gender( string label, float unscaledWidth, Gender current, out Gender gender )
        => ImGuiUtil.GenericEnumCombo( label, unscaledWidth * UiHelpers.Scale, current, out gender, RaceEnumExtensions.ToName, 1 );

    public static bool EqdpEquipSlot( string label, EquipSlot current, out EquipSlot slot )
        => ImGuiUtil.GenericEnumCombo( label, 100 * UiHelpers.Scale, current, out slot, EquipSlotExtensions.EqdpSlots, EquipSlotExtensions.ToName );

    public static bool EqpEquipSlot( string label, float width, EquipSlot current, out EquipSlot slot )
        => ImGuiUtil.GenericEnumCombo( label, width * UiHelpers.Scale, current, out slot, EquipSlotExtensions.EquipmentSlots, EquipSlotExtensions.ToName );

    public static bool AccessorySlot( string label, EquipSlot current, out EquipSlot slot )
        => ImGuiUtil.GenericEnumCombo( label, 100 * UiHelpers.Scale, current, out slot, EquipSlotExtensions.AccessorySlots, EquipSlotExtensions.ToName );

    public static bool SubRace( string label, SubRace current, out SubRace subRace )
        => ImGuiUtil.GenericEnumCombo( label, 150 * UiHelpers.Scale, current, out subRace, RaceEnumExtensions.ToName, 1 );

    public static bool RspAttribute( string label, RspAttribute current, out RspAttribute attribute )
        => ImGuiUtil.GenericEnumCombo( label, 200 * UiHelpers.Scale, current, out attribute,
            RspAttributeExtensions.ToFullString, 0, 1 );

    public static bool EstSlot( string label, EstManipulation.EstType current, out EstManipulation.EstType attribute )
        => ImGuiUtil.GenericEnumCombo( label, 200 * UiHelpers.Scale, current, out attribute );

    public static bool ImcType( string label, ObjectType current, out ObjectType type )
        => ImGuiUtil.GenericEnumCombo( label, 110 * UiHelpers.Scale, current, out type, ObjectTypeExtensions.ValidImcTypes,
            ObjectTypeExtensions.ToName );
}