using ImSharp;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab.Selector;

public readonly struct ModFilterToken() : IFilterToken<ModFilterTokenType, ModFilterToken>
{
    public string              Needle         { get; init; } = string.Empty;
    public ModFilterTokenType  Type           { get; init; }
    public ChangedItemIconFlag IconFlagFilter { get; init; }

    public bool Contains(ModFilterToken other)
    {
        if (Type != other.Type)
            return false;
        if (Type is ModFilterTokenType.Category)
            return IconFlagFilter == other.IconFlagFilter;

        return Needle.Contains(other.Needle);
    }

    public static bool ConvertToken(char tokenCharacter, out ModFilterTokenType type)
    {
        type = tokenCharacter switch
        {
            'c' or 'C' => ModFilterTokenType.ChangedItem,
            't' or 'T' => ModFilterTokenType.Tag,
            'n' or 'N' => ModFilterTokenType.Name,
            'a' or 'A' => ModFilterTokenType.Author,
            's' or 'S' => ModFilterTokenType.Category,
            _          => ModFilterTokenType.Default,
        };
        return type is not ModFilterTokenType.Default;
    }

    public static bool AllowsNone(ModFilterTokenType type)
        => type switch
        {
            ModFilterTokenType.Author      => true,
            ModFilterTokenType.ChangedItem => true,
            ModFilterTokenType.Tag         => true,
            ModFilterTokenType.Category    => true,
            _                              => false,
        };

    public static void ProcessList(List<ModFilterToken> list)
    {
        for (var i = 0; i < list.Count; ++i)
        {
            var entry = list[i];
            if (entry.Type is not ModFilterTokenType.Category)
                continue;

            if (ChangedItemDrawer.TryParsePartial(entry.Needle, out var icon))
                list[i] = entry with
                {
                    IconFlagFilter = icon,
                    Needle = string.Empty,
                };
            else
                list.RemoveAt(i--);
        }
    }
}
