using ImSharp;
using Penumbra.Api.Enums;
using Penumbra.GameData.Enums;
using Penumbra.Meta.Manipulations;

namespace Penumbra.UI;

public static class Combos
{
    public static readonly EnumCombo<ModelRace> ModelRace = new(ModelRaceExtensions.ToNameU8, ModelRaceExtensions.ToName);
    public static readonly EnumCombo<ModelRace> TailedRace = new(ModelRaceExtensions.ToNameU8, ModelRaceExtensions.ToName, null, [GameData.Enums.ModelRace.Miqote, GameData.Enums.ModelRace.AuRa, GameData.Enums.ModelRace.Hrothgar]);
    public static readonly EnumCombo<Gender>    Gender    = new(GenderExtensions.ToNameU8, GenderExtensions.ToName);

    public static readonly EnumCombo<EquipSlot> EqdpEquipSlot = new(EquipSlotExtensions.ToNameU8, EquipSlotExtensions.ToName, null,
        EquipSlotExtensions.EqdpSlots);

    public static readonly EnumCombo<EquipSlot> EqpEquipSlot = new(EquipSlotExtensions.ToNameU8, EquipSlotExtensions.ToName, null,
        EquipSlotExtensions.EquipmentSlots);

    public static readonly EnumCombo<EquipSlot> AccessorySlot = new(EquipSlotExtensions.ToNameU8, EquipSlotExtensions.ToName, null,
        EquipSlotExtensions.AccessorySlots);

    public static readonly EnumCombo<SubRace>      Clan    = new(SubRaceExtensions.ToNameU8, SubRaceExtensions.ToName);
    public static readonly EnumCombo<RspAttribute> RspType = new(RspAttributeExtensions.ToNameU8, RspAttributeExtensions.ToName);
    public static readonly EnumCombo<EstType>      EstSlot = new(EstTypeExtensions.ToNameU8, EstTypeExtensions.ToName);

    public static readonly EnumCombo<ObjectType> ImcType = new(ObjectTypeExtensions.ToNameU8, ObjectTypeExtensions.ToName, null,
        ObjectTypeExtensions.ValidImcTypes);

    public static readonly EnumCombo<ApiCollectionType> ApiCollectionType = new();
    public static readonly EnumCombo<TextureType>       TextureType       = new();
    public static readonly EnumCombo<ResourceType>      ResourceType      = new();
    public static readonly EnumCombo<TabType>           TabType           = new();
}
