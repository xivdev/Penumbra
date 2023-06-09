using System.Collections.Generic;
using System.Linq;
using Dalamud;
using Dalamud.Data;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace Penumbra.GameData.Data;

internal sealed class WeaponIdentificationList : KeyList<EquipItem>
{
    private const string Tag     = "WeaponIdentification";
    private const int    Version = 1;

    public WeaponIdentificationList(DalamudPluginInterface pi, ClientLanguage language, DataManager gameData)
        : base(pi, Tag, language, Version, CreateWeaponList(gameData, language))
    { }

    public IEnumerable<EquipItem> Between(SetId modelId)
        => Between(ToKey(modelId, 0, 0), ToKey(modelId, 0xFFFF, 0xFF));

    public IEnumerable<EquipItem> Between(SetId modelId, WeaponType type, byte variant = 0)
    {
        if (type == 0)
            return Between(ToKey(modelId, 0, 0), ToKey(modelId, 0xFFFF, 0xFF));
        if (variant == 0)
            return Between(ToKey(modelId, type, 0), ToKey(modelId, type, 0xFF));

        return Between(ToKey(modelId, type, variant), ToKey(modelId, type, variant));
    }

    public void Dispose(DalamudPluginInterface pi, ClientLanguage language)
        => DataSharer.DisposeTag(pi, Tag, language, Version);

    public static ulong ToKey(SetId modelId, WeaponType type, byte variant)
        => ((ulong)modelId << 32) | ((ulong)type << 16) | variant;

    public static ulong ToKey(EquipItem i)
        => ToKey(i.ModelId, i.WeaponType, i.Variant);

    protected override IEnumerable<ulong> ToKeys(EquipItem data)
    {
        yield return ToKey(data);
    }

    protected override bool ValidKey(ulong key)
        => key != 0;

    protected override int ValueKeySelector(EquipItem data)
        => (int)data.Id;

    private static IEnumerable<EquipItem> CreateWeaponList(DataManager gameData, ClientLanguage language)
        => gameData.GetExcelSheet<Item>(language)!.SelectMany(ToEquipItems);

    private static IEnumerable<EquipItem> ToEquipItems(Item item)
    {
        if ((EquipSlot)item.EquipSlotCategory.Row is not (EquipSlot.MainHand or EquipSlot.OffHand or EquipSlot.BothHand))
            yield break;

        if (item.ModelMain != 0)
            yield return EquipItem.FromMainhand(item);
        if (item.ModelSub != 0)
            yield return EquipItem.FromOffhand(item);
    }
}
