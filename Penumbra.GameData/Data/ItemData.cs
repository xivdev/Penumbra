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
using PseudoEquipItem = System.ValueTuple<string, uint, ushort, ushort, ushort, byte, byte>;

namespace Penumbra.GameData.Data;

public sealed class ItemData : DataSharer, IReadOnlyDictionary<FullEquipType, IReadOnlyList<EquipItem>>
{
    private readonly IReadOnlyDictionary<uint, PseudoEquipItem>    _mainItems;
    private readonly IReadOnlyDictionary<uint, PseudoEquipItem>    _offItems;
    private readonly IReadOnlyList<IReadOnlyList<PseudoEquipItem>> _byType;

    private static IReadOnlyList<IReadOnlyList<PseudoEquipItem>> CreateItems(DataManager dataManager, ClientLanguage language)
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
                    tmp[(int)type.Offhand()].Add(EquipItem.FromOffhand(item));
            }
            else if (type != FullEquipType.Unknown)
            {
                tmp[(int)type].Add(EquipItem.FromArmor(item));
            }
        }

        var ret = new IReadOnlyList<PseudoEquipItem>[tmp.Length];
        ret[0] = Array.Empty<PseudoEquipItem>();
        for (var i = 1; i < tmp.Length; ++i)
            ret[i] = tmp[i].OrderBy(item => item.Name).Select(s => (PseudoEquipItem)s).ToArray();

        return ret;
    }

    private static IReadOnlyDictionary<uint, PseudoEquipItem> CreateMainItems(IReadOnlyList<IReadOnlyList<PseudoEquipItem>> items)
    {
        var dict = new Dictionary<uint, PseudoEquipItem>(1024 * 4);
        foreach (var type in Enum.GetValues<FullEquipType>().Where(v => !FullEquipTypeExtensions.OffhandTypes.Contains(v)))
        {
            var list = items[(int)type];
            foreach (var item in list)
                dict.TryAdd(item.Item2, item);
        }

        dict.TrimExcess();
        return dict;
    }

    private static IReadOnlyDictionary<uint, PseudoEquipItem> CreateOffItems(IReadOnlyList<IReadOnlyList<PseudoEquipItem>> items)
    {
        var dict = new Dictionary<uint, PseudoEquipItem>(128);
        foreach (var type in FullEquipTypeExtensions.OffhandTypes)
        {
            var list = items[(int)type];
            foreach (var item in list)
                dict.TryAdd(item.Item2, item);
        }

        dict.TrimExcess();
        return dict;
    }

    public ItemData(DalamudPluginInterface pluginInterface, DataManager dataManager, ClientLanguage language)
        : base(pluginInterface, language, 1)
    {
        _byType    = TryCatchData("ItemList",     () => CreateItems(dataManager, language));
        _mainItems = TryCatchData("ItemDictMain", () => CreateMainItems(_byType));
        _offItems  = TryCatchData("ItemDictOff",  () => CreateOffItems(_byType));
    }

    protected override void DisposeInternal()
    {
        DisposeTag("ItemList");
        DisposeTag("ItemDictMain");
        DisposeTag("ItemDictOff");
    }

    public IEnumerator<KeyValuePair<FullEquipType, IReadOnlyList<EquipItem>>> GetEnumerator()
    {
        for (var i = 1; i < _byType.Count; ++i)
            yield return new KeyValuePair<FullEquipType, IReadOnlyList<EquipItem>>((FullEquipType)i, new EquipItemList(_byType[i]));
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => _byType.Count - 1;

    public bool ContainsKey(FullEquipType key)
        => (int)key < _byType.Count && key != FullEquipType.Unknown;

    public bool TryGetValue(FullEquipType key, out IReadOnlyList<EquipItem> value)
    {
        if (ContainsKey(key))
        {
            value = new EquipItemList(_byType[(int)key]);
            return true;
        }

        value = Array.Empty<EquipItem>();
        return false;
    }

    public IReadOnlyList<EquipItem> this[FullEquipType key]
        => TryGetValue(key, out var ret) ? ret : throw new IndexOutOfRangeException();

    public bool ContainsKey(uint key, bool main = true)
        => main ? _mainItems.ContainsKey(key) : _offItems.ContainsKey(key);

    public bool TryGetValue(uint key, out EquipItem value)
    {
        if (_mainItems.TryGetValue(key, out var v))
        {
            value = v;
            return true;
        }

        value = default;
        return false;
    }

    public IEnumerable<(uint, EquipItem)> AllItems(bool main)
        => (main ? _mainItems : _offItems).Select(i => (i.Key, (EquipItem)i.Value));

    public int TotalItemCount(bool main)
        => main ? _mainItems.Count : _offItems.Count;

    public bool TryGetValue(uint key, bool main, out EquipItem value)
    {
        var dict = main ? _mainItems : _offItems;
        if (dict.TryGetValue(key, out var v))
        {
            value = v;
            return true;
        }

        value = default;
        return false;
    }

    public IEnumerable<FullEquipType> Keys
        => Enum.GetValues<FullEquipType>().Skip(1);

    public IEnumerable<IReadOnlyList<EquipItem>> Values
        => _byType.Skip(1).Select(l => (IReadOnlyList<EquipItem>)new EquipItemList(l));

    private readonly struct EquipItemList : IReadOnlyList<EquipItem>
    {
        private readonly IReadOnlyList<PseudoEquipItem> _items;

        public EquipItemList(IReadOnlyList<PseudoEquipItem> items)
            => _items = items;

        public IEnumerator<EquipItem> GetEnumerator()
            => _items.Select(i => (EquipItem)i).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public int Count
            => _items.Count;

        public EquipItem this[int index]
            => _items[index];
    }
}
