using System.Runtime.InteropServices;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;

namespace Penumbra.GameData.Structs;

[StructLayout(LayoutKind.Sequential)]
public readonly struct EquipItem
{
    public readonly string        Name;
    public readonly uint          Id;
    public readonly ushort        IconId;
    public readonly SetId         ModelId;
    public readonly WeaponType    WeaponType;
    public readonly byte          Variant;
    public readonly FullEquipType Type;
    public readonly EquipSlot     Slot;

    public bool Valid
        => Type != FullEquipType.Unknown;

    public CharacterArmor Armor()
        => new(ModelId, Variant, 0);

    public CharacterArmor Armor(StainId stain)
        => new(ModelId, Variant, stain);

    public CharacterWeapon Weapon()
        => new(ModelId, WeaponType, Variant, 0);

    public CharacterWeapon Weapon(StainId stain)
        => new(ModelId, WeaponType, Variant, stain);

    public EquipItem()
        => Name = string.Empty;

    public EquipItem(string name, uint id, ushort iconId, SetId modelId, WeaponType weaponType, byte variant, FullEquipType type,
        EquipSlot slot)
    {
        Name       = string.Intern(name);
        Id         = id;
        IconId     = iconId;
        ModelId    = modelId;
        WeaponType = weaponType;
        Variant    = variant;
        Type       = type;
        Slot       = slot;
    }

    public static EquipItem FromArmor(Item item)
    {
        var type    = item.ToEquipType();
        var slot    = type.ToSlot();
        var name    = item.Name.ToDalamudString().TextValue;
        var id      = item.RowId;
        var icon    = item.Icon;
        var model   = (SetId)item.ModelMain;
        var weapon  = (WeaponType)0;
        var variant = (byte)(item.ModelMain >> 16);
        return new EquipItem(name, id, icon, model, weapon, variant, type, slot);
    }

    public static EquipItem FromMainhand(Item item)
    {
        var type    = item.ToEquipType();
        var name    = item.Name.ToDalamudString().TextValue;
        var id      = item.RowId;
        var icon    = item.Icon;
        var model   = (SetId)item.ModelMain;
        var weapon  = (WeaponType)(item.ModelMain >> 16);
        var variant = (byte)(item.ModelMain >> 32);
        return new EquipItem(name, id, icon, model, weapon, variant, type, EquipSlot.MainHand);
    }

    public static EquipItem FromOffhand(Item item)
    {
        var type    = item.ToEquipType().Offhand();
        var name    = item.Name.ToDalamudString().TextValue + type.OffhandTypeSuffix();
        var id      = item.RowId;
        var icon    = item.Icon;
        var model   = (SetId)item.ModelSub;
        var weapon  = (WeaponType)(item.ModelSub >> 16);
        var variant = (byte)(item.ModelSub >> 32);
        return new EquipItem(name, id, icon, model, weapon, variant, type, EquipSlot.OffHand);
    }
}
