using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dalamud;
using Dalamud.Data;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace Penumbra.GameData.Data;

public sealed class ItemData : DataSharer, IReadOnlyDictionary<FullEquipType, IReadOnlyList<EquipItem>>
{
    private readonly IReadOnlyList<IReadOnlyList<EquipItem>> _items;

    private static IReadOnlyList<IReadOnlyList<EquipItem>> CreateItems(DataManager dataManager, ClientLanguage language)
    {
        var tmp = Enum.GetValues<FullEquipType>().Select(_ => new List<EquipItem>(1024)).ToArray();

        var itemSheet = dataManager.GetExcelSheet<Item>(language)!;
        foreach (var item in itemSheet.Where(i => i.Name.RawData.Length > 1))
        {
            var type = item.ToEquipType();
            if (type.IsWeapon())
            {
                if (item.ModelMain != 0)
                    tmp[(int)type].Add(EquipItem.FromMainhand(item));
                if (item.ModelSub != 0)
                    tmp[(int)type].Add(EquipItem.FromOffhand(item));
            }
            else if (type != FullEquipType.Unknown)
            {
                tmp[(int)type].Add(EquipItem.FromArmor(item));
            }
        }

        var ret = new IReadOnlyList<EquipItem>[tmp.Length];
        ret[0] = Array.Empty<EquipItem>();
        for (var i = 1; i < tmp.Length; ++i)
            ret[i] = tmp[i].OrderBy(item => item.Name).ToArray();

        return ret;
    }

    public ItemData(DalamudPluginInterface pluginInterface, DataManager dataManager, ClientLanguage language)
        : base(pluginInterface, language, 1)
    {
        _items = TryCatchData("ItemList", () => CreateItems(dataManager, language));
    }

    protected override void DisposeInternal()
        => DisposeTag("ItemList");

    public IEnumerator<KeyValuePair<FullEquipType, IReadOnlyList<EquipItem>>> GetEnumerator()
    {
        for (var i = 1; i < _items.Count; ++i)
            yield return new KeyValuePair<FullEquipType, IReadOnlyList<EquipItem>>((FullEquipType)i, _items[i]);
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => _items.Count - 1;

    public bool ContainsKey(FullEquipType key)
        => (int)key < _items.Count && key != FullEquipType.Unknown;

    public bool TryGetValue(FullEquipType key, out IReadOnlyList<EquipItem> value)
    {
        if (ContainsKey(key))
        {
            value = _items[(int)key];
            return true;
        }

        value = _items[0];
        return false;
    }

    public IReadOnlyList<EquipItem> this[FullEquipType key]
        => TryGetValue(key, out var ret) ? ret : throw new IndexOutOfRangeException();

    public IEnumerable<FullEquipType> Keys
        => Enum.GetValues<FullEquipType>().Skip(1);

    public IEnumerable<IReadOnlyList<EquipItem>> Values
        => _items.Skip(1);
}
