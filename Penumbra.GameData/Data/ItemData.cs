using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dalamud;
using Dalamud.Data;
using Dalamud.Plugin;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;

namespace Penumbra.GameData.Data;

public sealed class ItemData : DataSharer, IReadOnlyDictionary<FullEquipType, IReadOnlyList<Item>>
{
    private readonly IReadOnlyList<IReadOnlyList<Item>> _items;

    private static IReadOnlyList<IReadOnlyList<Item>> CreateItems(DataManager dataManager, ClientLanguage language)
    {
        var tmp = Enum.GetValues<FullEquipType>().Select(t => new List<Item>(1024)).ToArray();

        var itemSheet = dataManager.GetExcelSheet<Item>(language)!;
        foreach (var item in itemSheet)
        {
            var type = item.ToEquipType();
            if (type != FullEquipType.Unknown && item.Name.RawData.Length > 1)
                tmp[(int)type].Add(item);
        }

        var ret = new IReadOnlyList<Item>[tmp.Length];
        ret[0] = Array.Empty<Item>();
        for (var i = 1; i < tmp.Length; ++i)
            ret[i] = tmp[i].OrderBy(item => item.Name.ToDalamudString().TextValue).ToArray();

        return ret;
    }

    public ItemData(DalamudPluginInterface pluginInterface, DataManager dataManager, ClientLanguage language)
        : base(pluginInterface, language, 1)
    {
        _items = TryCatchData("ItemList", () => CreateItems(dataManager, language));
    }

    protected override void DisposeInternal()
        => DisposeTag("ItemList");

    public IEnumerator<KeyValuePair<FullEquipType, IReadOnlyList<Item>>> GetEnumerator()
    {
        for (var i = 1; i < _items.Count; ++i)
            yield return new KeyValuePair<FullEquipType, IReadOnlyList<Item>>((FullEquipType)i, _items[i]);
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => _items.Count - 1;

    public bool ContainsKey(FullEquipType key)
        => (int)key < _items.Count && key != FullEquipType.Unknown;

    public bool TryGetValue(FullEquipType key, out IReadOnlyList<Item> value)
    {
        if (ContainsKey(key))
        {
            value = _items[(int)key];
            return true;
        }

        value = _items[0];
        return false;
    }

    public IReadOnlyList<Item> this[FullEquipType key]
        => TryGetValue(key, out var ret) ? ret : throw new IndexOutOfRangeException();

    public IEnumerable<FullEquipType> Keys
        => Enum.GetValues<FullEquipType>().Skip(1);

    public IEnumerable<IReadOnlyList<Item>> Values
        => _items.Skip(1);
}
