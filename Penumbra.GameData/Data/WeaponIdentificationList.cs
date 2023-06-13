using System.Collections.Generic;
using System.Linq;
using Dalamud;
using Dalamud.Data;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using PseudoEquipItem = System.ValueTuple<string, uint, ushort, ushort, ushort, byte, byte>;

namespace Penumbra.GameData.Data;

internal sealed class WeaponIdentificationList : KeyList<PseudoEquipItem>
{
    private const string Tag     = "WeaponIdentification";
    private const int    Version = 1;

    public WeaponIdentificationList(DalamudPluginInterface pi, ClientLanguage language, DataManager gameData)
        : base(pi, Tag, language, Version, CreateWeaponList(gameData, language))
    { }

    public IEnumerable<EquipItem> Between(SetId modelId)
        => Between(ToKey(modelId, 0, 0), ToKey(modelId, 0xFFFF, 0xFF)).Select(e => (EquipItem)e);

    public IEnumerable<EquipItem> Between(SetId modelId, WeaponType type, byte variant = 0)
    {
        if (type == 0)
            return Between(ToKey(modelId, 0, 0), ToKey(modelId, 0xFFFF, 0xFF)).Select(e => (EquipItem)e);
        if (variant == 0)
            return Between(ToKey(modelId, type, 0), ToKey(modelId, type, 0xFF)).Select(e => (EquipItem)e);

        return Between(ToKey(modelId, type, variant), ToKey(modelId, type, variant)).Select(e => (EquipItem)e);
    }

    public void Dispose(DalamudPluginInterface pi, ClientLanguage language)
        => DataSharer.DisposeTag(pi, Tag, language, Version);

    public static ulong ToKey(SetId modelId, WeaponType type, byte variant)
        => ((ulong)modelId << 32) | ((ulong)type << 16) | variant;

    public static ulong ToKey(EquipItem i)
        => ToKey(i.ModelId, i.WeaponType, i.Variant);

    protected override IEnumerable<ulong> ToKeys(PseudoEquipItem data)
    {
        yield return ToKey(data);
    }

    protected override bool ValidKey(ulong key)
        => key != 0;

    protected override int ValueKeySelector(PseudoEquipItem data)
        => (int)data.Item2;

    private static IEnumerable<PseudoEquipItem> CreateWeaponList(DataManager gameData, ClientLanguage language)
        => gameData.GetExcelSheet<Item>(language)!.SelectMany(ToEquipItems);

    private static IEnumerable<PseudoEquipItem> ToEquipItems(Item item)
    {
        if ((EquipSlot)item.EquipSlotCategory.Row is not (EquipSlot.MainHand or EquipSlot.OffHand or EquipSlot.BothHand))
            yield break;

        if (item.ModelMain != 0)
            yield return (PseudoEquipItem)EquipItem.FromMainhand(item);
        if (item.ModelSub != 0)
            yield return (PseudoEquipItem)EquipItem.FromOffhand(item);
    }
}
