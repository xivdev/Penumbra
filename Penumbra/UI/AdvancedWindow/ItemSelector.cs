using ImSharp;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Mods;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab;

namespace Penumbra.UI.AdvancedWindow;

public sealed class ItemSelector(ActiveCollections collections, ItemData data, ModFileSystemSelector? selector, FullEquipType type)
    : FilterComboBase<ItemSelector.CacheItem>(new ItemFilter())
{
    public EquipItem CurrentSelection = new(string.Empty, default, default, default, default, default, FullEquipType.Unknown, default, default,
        default);

    public sealed record CacheItem(EquipItem Item, StringPair Name, Vector4 Color, bool InCurrentMod, StringU8[] CollectionMods)
    {
        public Vector4 Color { get; set; } = Color;

        public CacheItem(EquipItem item, Mod currentMod, ModCollection currentCollection)
            : this(item, new StringPair(item.Name), Im.Style[ImGuiColor.Text], currentMod.ChangedItems.Any(c => c.Key == item.Name),
                ConvertCollection(item, currentCollection))
        {
            if (InCurrentMod)
                Color = ColorId.ResTreeLocalPlayer.Value().ToVector();
            else if (CollectionMods.Length > 0)
                Color = ColorId.ResTreeNonNetworked.Value().ToVector();
        }

        public CacheItem(EquipItem item, ModCollection currentCollection)
            : this(item, new StringPair(item.Name), Im.Style[ImGuiColor.Text], false, ConvertCollection(item, currentCollection))
        {
            if (CollectionMods.Length > 0)
                Color = ColorId.ResTreeNonNetworked.Value().ToVector();
        }

        private static StringU8[] ConvertCollection(in EquipItem item, ModCollection collection)
        {
            if (!collection.ChangedItems.TryGetValue(item.Name, out var mods))
                return [];

            var ret = new StringU8[mods.Item1.Count];
            for (var i = 0; i < mods.Item1.Count; ++i)
                ret[i] = new StringU8(mods.Item1[i].Name);
            return ret;
        }
    }

    private sealed class ItemFilter : PartwiseFilterBase<CacheItem>
    {
        protected override string ToFilterString(in CacheItem item, int globalIndex)
            => item.Name.Utf16;
    }


    protected override IEnumerable<CacheItem> GetItems()
    {
        var list = data.ByType[type];
        if (selector?.Selected is { } currentMod && currentMod.ChangedItems.Values.Any(c => c is IdentifiedItem i && i.Item.Type == type))
            return list.Select(item => new CacheItem(item, currentMod, collections.Current)).OrderByDescending(i => i.InCurrentMod)
                .ThenByDescending(i => i.CollectionMods.Length);

        if (selector is null)
            return list.Select(item => new CacheItem(item, collections.Current)).OrderBy(i => i.CollectionMods.Length);

        return list.Select(item => new CacheItem(item, collections.Current)).OrderByDescending(i => i.CollectionMods.Length);
    }

    protected override float ItemHeight
        => Im.Style.TextHeightWithSpacing;

    protected override bool DrawItem(in CacheItem item, int globalIndex, bool selected)
    {
        using var color = item.Color.W > 0 ? ImGuiColor.Text.Push(item.Color) : null;
        var       ret   = Im.Selectable(item.Name.Utf8, selected);
        if (item.CollectionMods.Length > 0 && Im.Item.Hovered())
        {
            using var style = Im.Style.PushDefault(ImStyleDouble.WindowPadding);
            using var tt    = Im.Tooltip.Begin();
            foreach (var mod in item.CollectionMods)
                Im.Text(mod);
        }

        if (ret)
            CurrentSelection = item.Item;
        return ret;
    }

    protected override bool IsSelected(CacheItem item, int globalIndex)
        => item.Item.Equals(CurrentSelection);
}
