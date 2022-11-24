using System.Collections.Generic;
using System.Linq;
using Dalamud;
using Dalamud.Data;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace Penumbra.GameData.Data;

internal sealed class WeaponIdentificationList : KeyList<Item>
{
    private const string Tag     = "WeaponIdentification";
    private const int    Version = 1;

    public WeaponIdentificationList(DalamudPluginInterface pi, ClientLanguage language, DataManager gameData)
        : base(pi, Tag, language, Version, CreateWeaponList(gameData, language))
    { }

    public IEnumerable<Item> Between(SetId modelId)
        => Between(ToKey(modelId, 0, 0), ToKey(modelId, 0xFFFF, 0xFF));

    public IEnumerable<Item> Between(SetId modelId, WeaponType type, byte variant = 0)
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

    public static ulong ToKey(Item i, bool offhand)
    {
        var quad = offhand ? (Lumina.Data.Parsing.Quad)i.ModelSub : (Lumina.Data.Parsing.Quad)i.ModelMain;
        return ToKey(quad.A, quad.B, (byte)quad.C);
    }

    protected override IEnumerable<ulong> ToKeys(Item i)
    {
        var key1 = 0ul;
        if (i.ModelMain != 0)
        {
            key1 = ToKey(i, false);
            yield return key1;
        }

        if (i.ModelSub != 0)
        {
            var key2 = ToKey(i, true);
            if (key1 != key2)
                yield return key2;
        }
    }

    protected override bool ValidKey(ulong key)
        => key != 0;

    protected override int ValueKeySelector(Item data)
        => (int)data.RowId;

    private static IEnumerable<Item> CreateWeaponList(DataManager gameData, ClientLanguage language)
    {
        var items = gameData.GetExcelSheet<Item>(language)!;
        return items.Where(i => (EquipSlot)i.EquipSlotCategory.Row is EquipSlot.MainHand or EquipSlot.OffHand or EquipSlot.BothHand);
    }
}
